using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Combat;

/// <summary>
/// 战斗态容器,逐字移植核心结构（<c>MegaCrit.Sts2.Core.Combat.CombatState</c>）。偏离 #37：
/// 无 EncounterModel/Modifiers/BadgeModels/MultiplayerScalingModel；显式怪物批次直接逐只注册；
/// IterateHookListeners 不链接 ModHelper 的 mod 订阅者。
/// </summary>
public sealed partial class CombatState : ICombatState
{
    private readonly List<Creature> _allies = new();
    private readonly List<Creature> _enemies = new();
    private readonly List<Creature> _spawnedEnemies = new();
    private readonly List<Creature> _escapedCreatures = new();
    private readonly List<Creature> _removedCreatures = new();
    private CombatEngine? _engine;
    private uint _nextCreatureId;
    private int _shuffleOrdinal;
    private int _potionUseOrdinal;
    private string _semanticCombatKey;

    public CombatState(IRunState runState)
        : this(runState, Array.Empty<string>())
    {
    }

    internal CombatState(IRunState runState, IReadOnlyList<string> encounterSlots)
    {
        RunState = runState;
        _encounterSlots = encounterSlots.ToArray();
        _semanticCombatKey = runState is Runs.RunState concreteRun
            ? concreteRun.SemanticLocationKey
            : $"floor={runState.TotalFloor}/room_kind={runState.CurrentRoom?.RoomType.ToString() ?? "none"}/room_id={runState.CurrentRoom?.Id ?? 0}";
    }

    private readonly string[] _encounterSlots;

    /// <summary>Sorts encounter slots without changing registration IDs or spawn history.</summary>
    internal void SortEnemiesBySlotName()
    {
        if (_encounterSlots.Length == 0) return;
        _enemies.Sort((a, b) => Array.IndexOf(_encounterSlots, a.SlotName)
            - Array.IndexOf(_encounterSlots, b.SlotName));
    }

    public IRunState RunState { get; }

    public IReadOnlyList<Creature> Allies => _allies;

    // Model-less standalone creatures are test-only player proxies; production allies are players or pets.
    public IReadOnlyList<Creature> PlayerCreatures =>
        _allies.Where(creature => creature.IsPlayer || (!creature.IsMonster && !creature.IsPet)).ToList();

    public IReadOnlyList<Creature> Enemies => _enemies;

    /// <summary>Every enemy registered in this combat, including summons, deaths, removals, and escapes.</summary>
    public IReadOnlyList<Creature> SpawnedEnemies => _spawnedEnemies;

    // EncounterDefinition is shared across combats, so the mutable GremlinMercNormal flag lives here.
    public bool GoldWasStolen { get; private set; }

    internal void MarkGoldStolen() => GoldWasStolen = true;

    public IReadOnlyList<Creature> Creatures => _allies.Concat(_enemies).ToList();

    public IReadOnlyList<Creature> EscapedCreatures => _escapedCreatures;

    // Reference-only archive for clone/fingerprint rebinding, never active combat membership.
    internal IReadOnlyList<Creature> RemovedCreatures => _removedCreatures;

    public IReadOnlyList<Player> Players => RunState.Players;

    public IReadOnlyList<Creature> HittableEnemies => _enemies.Where(creature => creature.IsHittable).ToList();

    public CombatDamageHistory DamageHistory { get; private set; } = new();

    public CombatSemanticHistory SemanticHistory { get; private set; } = new();

    /// <summary>Optional passive prediction notifications. Branch owners attach their own sink after cloning.</summary>
    public ICombatPredictionSink? PredictionSink { get; set; }

    public int CardsExhaustedThisCombat => SemanticHistory.CardsExhaustedThisCombat;

    public int CountCardsExhaustedThisTurn(Player actor) =>
        SemanticHistory.CountCardsExhaustedThisTurn(this, actor);

    public int CountOtherQualifyingBlockGainsThisTurn(Creature owner, CardPlay? currentPlay) =>
        SemanticHistory.CountOtherQualifyingBlockGainsThisTurn(this, owner, currentPlay);

    public ICardSelectionDecisionSource CardSelectionSource { get; set; } =
        RejectingCardSelectionDecisionSource.Instance;

    public CombatSide CurrentSide { get; set; } = CombatSide.Player;

    public int RoundNumber { get; set; } = 1;

    /// <summary>True only while the engine is resolving a player extra turn.</summary>
    public bool IsPlayerExtraTurn { get; internal set; }

    /// <summary>本状态是搜索投影（由 <see cref="Clone"/> 产生），不是真实战斗。
    /// 投影里禁止任何越过克隆图、写到共享 <c>RunState</c> 的副作用——
    /// 见 Plan 08b-3h 与 <see cref="Sts2Sim.Core.Models.Powers.SwipePower"/>。</summary>
    public bool IsProjection { get; private set; }

    internal Rng NextShuffleRng()
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return RunState.Rng.Shuffle;
        }

        int ordinal = _shuffleOrdinal++;
        return RunState.Rng.ForSemanticKey(
            RunRngType.Shuffle,
            $"{_semanticCombatKey}/shuffle_ordinal={ordinal}");
    }

    /// <summary>
    /// Returns an independent generator for the next combat shuffle without consuming that shuffle.
    /// Sequential fidelity mode clones the live stream exactly. Keyed label mode re-derives the
    /// generator for the current semantic shuffle ordinal; both paths therefore project the same
    /// shuffle that <see cref="NextShuffleRng"/> will later consume.
    /// </summary>
    public Rng CloneNextShuffleRngForProjection()
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return RunState.Rng.Shuffle.CloneExact();
        }

        return RunState.Rng.ForSemanticKey(
            RunRngType.Shuffle,
            $"{_semanticCombatKey}/shuffle_ordinal={_shuffleOrdinal}");
    }

    internal Rng MonsterAiRng(Creature enemy)
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return RunState.Rng.MonsterAi;
        }

        return RunState.Rng.ForSemanticKey(
            RunRngType.MonsterAi,
            $"{_semanticCombatKey}/enemy_uid={enemy.CombatId}/turn={RoundNumber}");
    }

    internal IDisposable BeginCardRngScope(CardModel card)
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return EmptyRngScope.Instance;
        }

        return BeginRngScopes(
            $"{_semanticCombatKey}/turn={RoundNumber}" +
            $"/source_card_uid={card.Id.Entry}/play_ordinal={card.Owner.PlayerCombatState?.CardPlaysStartedThisTurn ?? 0}",
            [card.Owner]);
    }

    internal IDisposable BeginPotionRngScope(PotionModel potion)
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return EmptyRngScope.Instance;
        }

        int ordinal = _potionUseOrdinal++;
        return BeginRngScopes(
            $"{_semanticCombatKey}/turn={RoundNumber}" +
            $"/source_potion_uid={potion.Id.Entry}/use_ordinal={ordinal}",
            [potion.Owner]);
    }

    internal IDisposable BeginMonsterRngScope(Creature enemy)
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return EmptyRngScope.Instance;
        }

        return BeginRngScopes(
            $"{_semanticCombatKey}/enemy_uid={enemy.CombatId}/turn={RoundNumber}/move",
            Players);
    }

    internal IDisposable BeginPhaseRngScope(string phase)
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return EmptyRngScope.Instance;
        }

        return BeginRngScopes(
            $"{_semanticCombatKey}/turn={RoundNumber}/side={CurrentSide}/phase={phase}",
            Players);
    }

    private IDisposable BeginRngScopes(string semanticKey, IEnumerable<Player> players)
    {
        if (!RunState.Rng.UsesSemanticKeys)
        {
            return EmptyRngScope.Instance;
        }

        var scopes = new List<IDisposable> { RunState.Rng.BeginSemanticScope(semanticKey) };
        scopes.AddRange(players.Distinct().Select(
            player => player.PlayerRng.BeginSemanticScope(semanticKey)));
        return new CompositeRngScope(scopes);
    }

    private sealed class CompositeRngScope(IReadOnlyList<IDisposable> scopes) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                scopes[index].Dispose();
            }
            _disposed = true;
        }
    }

    private sealed class EmptyRngScope : IDisposable
    {
        public static readonly EmptyRngScope Instance = new();
        public void Dispose() { }
    }

    public void AddPlayerCreature(Creature creature)
    {
        creature.CombatState = this;
        creature.CombatId = _nextCreatureId++;
        AddCreature(creature);
    }

    /// <summary>
    /// 创建并附着怪物但不加入任一方列表；调用 AddCreature 后才参与目标选择与钩子枚举。
    /// 怪物个体 RNG 按原生公式由 run 种子、当前地图列/行、
    /// 当前幕索引与 CombatId 共同派生；精简 IRunState 没有位置元数据时三项按零处理。
    /// </summary>
    public Creature CreateCreature(MonsterModel monster, CombatSide side, string? slotName)
    {
        monster.AssertMutable();

        uint combatId = _nextCreatureId;
        var creature = new Creature(monster, side)
        {
            CombatId = combatId,
            CombatState = this,
        };
        _nextCreatureId++;

        creature.AssignSlotName(slotName);
        monster.AssignCreature(creature);
        monster.RunRng = RunState.Rng;
        MapLocation location = RunState switch
        {
            Runs.RunState concreteRun => concreteRun.MapLocation,
            CombatRunStateSnapshot snapshot => snapshot.MapLocation,
            _ => default,
        };
        monster.Rng = new Rng((ulong)(
            (long)RunState.Rng.Seed
            + (long)(location.coord?.col ?? 0)
            + (location.coord?.row ?? 0)
            + location.actIndex
            + combatId));

        if (side == CombatSide.Enemy)
        {
            creature.SetUniqueMonsterHpValue(
                _enemies,
                RunState.Rng.ForSemanticKey(
                    RunRngType.Niche,
                    $"{_semanticCombatKey}/enemy_uid={combatId}/monster_hp"));
        }

        return creature;
    }

    public void AddCreature(Creature creature)
    {
        if (creature.CombatState != this)
            throw new InvalidOperationException("Creature was created for a different combat.");
        if (ContainsCreature(creature))
            throw new InvalidOperationException("Creature is already in this combat.");
        (creature.Side == CombatSide.Enemy ? _enemies : _allies).Add(creature);
        if (creature.Side == CombatSide.Enemy) _spawnedEnemies.Add(creature);
    }

    public Creature AddMonster(MonsterModel monster, CombatSide side) => AddMonster(monster, side, null);

    public Creature AddMonster(MonsterModel monster, CombatSide side, string? slotName)
    {
        Creature creature = CreateCreature(monster, side, slotName);
        AddCreature(creature);
        return creature;
    }

    public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) =>
        creature.Side == CombatSide.Player ? _enemies : _allies;

    public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) =>
        side == CombatSide.Player ? _allies : _enemies;

    public bool ContainsCreature(Creature creature) => _allies.Contains(creature) || _enemies.Contains(creature);

    /// <summary>Removes a creature from combat without recording an escape.</summary>
    public void RemoveCreature(Creature creature, bool unattach = true)
    {
        if (creature.CombatState is null)
        {
            return;
        }

        if (creature.CombatState != this)
        {
            throw new InvalidOperationException("Creature is in a different combat.");
        }

        if (!_enemies.Remove(creature) && !_allies.Remove(creature))
        {
            throw new InvalidOperationException($"Removed creature '{creature}' was not found.");
        }

        _removedCreatures.Add(creature);
        if (unattach)
        {
            creature.CombatState = null;
        }
    }

    public void CreatureEscaped(Creature creature)
    {
        ArgumentNullException.ThrowIfNull(creature);
        if (!ContainsCreature(creature))
        {
            return;
        }

        _allies.Remove(creature);
        _enemies.Remove(creature);
        _escapedCreatures.Add(creature);
        creature.CombatState = null;
    }

    /// <summary>Production states derive liveness from their engine; standalone states remain live for structural tests.</summary>
    public bool IsLiveCombat() => _engine?.IsInProgress ?? true;

    // 没有挂引擎的独立战斗状态（测试夹具）视为仍在进行，与 IsLiveCombat 的默认一致。
    public bool IsOverOrEnding() => _engine?.IsOverOrEnding ?? false;
    internal ICombatObserver? Observer => _engine?.Observer;


    internal void AttachEngine(CombatEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        if (_engine is not null && !ReferenceEquals(_engine, engine))
        {
            throw new InvalidOperationException("A combat state cannot be attached to multiple engines.");
        }

        _engine = engine;
    }

    /// <summary>
    /// hook 监听器遍历顺序：先盟友后敌人；每个生物先它的 powers,再是它的怪物模型（怪物侧）或
    /// 手牌/抽/弃/消耗/出牌堆里的全部卡牌及其附魔（玩家侧,仅当 IsActiveForHooks 时)。逐字对照原文结构移植
    /// （<c>MegaCrit.Sts2.Core.Combat.CombatState.IterateHookListeners</c>），是确定性契约的一部分。
    /// </summary>
    public IEnumerable<AbstractModel> IterateHookListeners() => IterateHookListeners(includePlayerInventory: false);

    // Block retention needs powers-before-relics ordering. Other dispatchers retain
    // the inventory-free iterator composed with RunState's existing wrapper.
    internal IEnumerable<AbstractModel> IterateHookListeners(bool includePlayerInventory)
    {
        // Build the whole dispatch snapshot before any hook runs. Intermediate copies of
        // creature, orb, and pile collections are unnecessary during this synchronous pass.
        var listeners = new List<AbstractModel>();
        foreach (Creature creature in _allies)
        {
            AppendCreatureHookListeners(listeners, creature, includePlayerInventory);
        }

        foreach (Creature creature in _enemies)
        {
            AppendCreatureHookListeners(listeners, creature, includePlayerInventory);
        }

        return listeners;
    }

    private static void AppendCreatureHookListeners(
        List<AbstractModel> listeners, Creature creature, bool includePlayerInventory)
    {
        listeners.AddRange(creature.Powers);
        if (creature.IsMonster)
        {
            listeners.Add(creature.Monster!);
            return;
        }

        if (!creature.IsPlayer || !creature.Player!.IsActiveForHooks)
        {
            return;
        }

        Player player = creature.Player;
        if (includePlayerInventory)
        {
            foreach (RelicModel relic in player.Relics)
            {
                if (!relic.IsMelted)
                {
                    listeners.Add(relic);
                }
            }

            foreach (PotionModel? potion in player.PotionSlots)
            {
                if (potion is not null)
                {
                    listeners.Add(potion);
                }
            }
        }

        PlayerCombatState? playerCombatState = player.PlayerCombatState;
        if (playerCombatState is null)
        {
            return;
        }

        listeners.AddRange(playerCombatState.OrbQueue.Orbs);
        AppendPileHookListeners(listeners, playerCombatState.Hand);
        AppendPileHookListeners(listeners, playerCombatState.DrawPile);
        AppendPileHookListeners(listeners, playerCombatState.DiscardPile);
        AppendPileHookListeners(listeners, playerCombatState.ExhaustPile);
        AppendPileHookListeners(listeners, playerCombatState.PlayPile);
    }

    private static void AppendPileHookListeners(List<AbstractModel> listeners, CardPile pile)
    {
        foreach (CardModel card in pile.Cards)
        {
            listeners.Add(card);
            foreach (EnchantmentModel enchantment in card.Enchantments)
            {
                listeners.Add(enchantment);
            }

            if (card.Affliction is not null)
            {
                listeners.Add(card.Affliction);
            }
        }
    }
}
