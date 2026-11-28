using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Combat;

public sealed partial class CombatState
{
    /// <summary>
    /// The engine attached to this state. A cloned state receives an independently mutable engine with no observer.
    /// Search code uses it to advance a branch without sending duplicate reporting callbacks.
    /// </summary>
    public CombatEngine? Engine => _engine;

    /// <summary>
    /// Creates an independent combat graph for speculative simulation. Canonical character metadata,
    /// the immutable ascension configuration, and the containing room identity are intentionally shared;
    /// every combat-mutable model, creature, pile, player RNG, monster RNG, and run RNG is cloned.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 本方法始终走 <see cref="Runs.RunRngSet.CloneExact"/>：战斗搜索的每个分支都必须看到
    /// 与真实流一致的未来，这是投影洗牌等既有启发式成立的前提。
    /// 在显式 keyed 标签模式下，<c>CloneExact</c> 的“未来一致”指保留相同的 run seed、
    /// keyed 派生语义与已记录轨迹；克隆和原状态之后以同一 semantic key 取数会得到相同结果，
    /// 但各自追加独立的轨迹。它不会退回共享顺序 counter，也不会把搜索分支的记录写回真实战斗。
    /// </para>
    /// <para>
    /// 重播种的对应物是 <see cref="CloneForPublicWorld"/>（Plan 08b-1 当时刻意不提供，等真实消费者；
    /// 消费者现为 Boss 战公开信息搜索，Plan 09-M stupa P4a）。两者是互斥语义、刻意分开的方法名：
    /// 本方法给完全信息的战斗求解器（AGENTS.md §3 例外①），公开世界克隆给只许用公开信息的推演。
    /// 不存在"只重播种一部分流"的战斗克隆。
    /// </para>
    /// </remarks>
    public CombatState Clone() => Clone(out _);

    public CombatState Clone(out CombatCloneMap map)
    {
        var runState = new CombatRunStateSnapshot(RunState, RunState.Rng.CloneExact());
        int estimatedCards = EstimatePlayerCardsCount(Players);
        var cardMap = new Dictionary<CardModel, CardModel>(estimatedCards + 4, ReferenceEqualityComparer.Instance);
        var playerMap = new Dictionary<Player, Player>(Players.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < Players.Count; i++)
        {
            Player player = Players[i];
            Player clonedPlayer = player.CloneForCombat(runState, cardMap);
            playerMap.Add(player, clonedPlayer);
            runState.AddPlayer(clonedPlayer);
        }

        return CloneProjection(runState, playerMap, cardMap, out map);
    }

    private static int EstimatePlayerCardsCount(IReadOnlyList<Player> players)
    {
        int total = 0;
        for (int i = 0; i < players.Count; i++)
        {
            Player p = players[i];
            total += p.Deck.CardList.Count;
            if (p.PlayerCombatState is { } pcs)
            {
                total += pcs.Hand.CardList.Count
                    + pcs.DrawPile.CardList.Count
                    + pcs.DiscardPile.CardList.Count
                    + pcs.ExhaustPile.CardList.Count
                    + pcs.PlayPile.CardList.Count;
            }
        }
        return total;
    }

    /// <summary>
    /// 公开信息世界：与本战斗公开状态相同、不共享任何隐藏未来的投影克隆（偏离 #189 在战斗内的组装）。
    /// 只由公开状态与 <paramref name="worldSeed"/> 决定，不读真实种子、真实 RNG 流、真实抽牌顺序或遗物袋顺序。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>run RNG：全新的 <see cref="RunRngSet"/>（种子与全部流都来自世界种子）。</item>
    /// <item>玩家 RNG：全新的 <see cref="PlayerRngSet"/>，种子独立于 run 种子。</item>
    /// <item>怪物私有 RNG：按世界种子与 <see cref="Creature.CombatId"/> 重起。</item>
    /// <item>遗物袋：内容不变，按遗物 ID 排序后重洗。</item>
    /// <item>抽牌堆：内容不变；先按牌面状态（同状态再按牌组位置）排成与原顺序无关的规范序，再用世界种子洗牌。</item>
    /// </list>
    /// 手牌、弃牌、消耗、血量、能力、怪物当前意图等公开状态原样保留。带语义键的 RNG 集合（偏离 #329）不支持。
    /// 完全相同且都没有牌组对应物的战斗内生成牌之间无法再分先后，它们的相对位置沿用原顺序。
    /// </remarks>
    public CombatState CloneForPublicWorld(ulong worldSeed)
    {
        if (RunState.Rng.UsesSemanticKeys || Players.Any(player => player.PlayerRng.UsesSemanticKeys))
            throw new NotSupportedException("A public-information world cannot be built from keyed RNG sets.");

        var runState = new CombatRunStateSnapshot(
            RunState, new RunRngSet($"PUBLICWORLD{PublicWorldSeed(worldSeed, 0):X16}"));
        var cardMap = new Dictionary<CardModel, CardModel>(ReferenceEqualityComparer.Instance);
        var playerMap = new Dictionary<Player, Player>(ReferenceEqualityComparer.Instance);
        foreach (Player player in Players)
        {
            Player clonedPlayer = player.CloneForPublicWorld(
                runState, cardMap, PublicWorldSeed(worldSeed, 1), new Rng(PublicWorldSeed(worldSeed, 2)));
            playerMap.Add(player, clonedPlayer);
            runState.AddPlayer(clonedPlayer);
        }

        CombatState world = CloneProjection(runState, playerMap, cardMap, out _);
        foreach (Creature creature in world.EnumerateCloneCreatures())
            if (creature.Monster is { } monster)
                monster.Rng = new Rng(PublicWorldSeed(worldSeed, 3) + (creature.CombatId ?? 0));

        ulong pileStream = 4;
        foreach (Player player in world.Players)
        {
            ulong pileSeed = PublicWorldSeed(worldSeed, pileStream++);
            if (player.PlayerCombatState is not { } combat) continue;
            List<CardModel> deck = player.Deck.Cards.ToList();
            List<CardModel> cards = combat.DrawPile.Cards
                .Select(card =>
                {
                    var builder = new CombatStateDescriptionBuilder();
                    CombatStateDescription.AppendCardState(ref builder, card, world);
                    return (Card: card, Face: builder.Build(),
                        DeckIndex: card.DeckVersion is { } deckVersion ? deck.IndexOf(deckVersion) : -1);
                })
                .OrderBy(entry => entry.Face.First).ThenBy(entry => entry.Face.Second)
                .ThenBy(entry => entry.DeckIndex)
                .Select(entry => entry.Card).ToList();
            cards.UnstableShuffle(new Rng(pileSeed));
            // 只换顺序：逐张移到底部，不经过会触发入堆副作用的 AddInternal。
            foreach (CardModel card in cards) combat.DrawPile.MoveToBottomInternal(card);
        }
        return world;
    }

    /// <summary>SplitMix64(世界种子, 流序号)：一个公开世界里每条隐藏流各自独立的种子。</summary>
    private static ulong PublicWorldSeed(ulong worldSeed, ulong stream)
    {
        ulong z = worldSeed + (stream + 1) * 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private CombatState CloneProjection(
        CombatRunStateSnapshot runState,
        Dictionary<Player, Player> playerMap,
        Dictionary<CardModel, CardModel> cardMap,
        out CombatCloneMap map)
    {
        CombatState clone = NewCloneState(runState, projection: true);
        var states = new CloneStates(this, clone);
        int estimatedCreatures = _allies.Count + _enemies.Count + _escapedCreatures.Count + _removedCreatures.Count;
        var creatures = new Dictionary<Creature, Creature>(estimatedCreatures + 2, ReferenceEqualityComparer.Instance);
        CloneGraph(runState, playerMap, cardMap, states, creatures, powerMap: null, projection: true);
        map = new CombatCloneMap(cardMap, playerMap, creatures);
        return clone;
    }

    // One context per run graph, constructed after every room/offer/private-card data root is mapped.
    internal sealed class RunCloneContext
    {
        internal readonly RunState TargetRun;
        internal readonly IReadOnlyDictionary<Player, Player> Players;
        internal readonly Dictionary<CardModel, CardModel> Cards;
        internal readonly Dictionary<CombatState, CombatState> States = new(ReferenceEqualityComparer.Instance);
        internal readonly Dictionary<Creature, Creature> Creatures = new(ReferenceEqualityComparer.Instance);
        internal readonly Dictionary<PowerModel, PowerModel> Powers = new(ReferenceEqualityComparer.Instance);
        internal readonly CombatCloneMap Map;

        internal RunCloneContext(RunState targetRun,
            IReadOnlyDictionary<Player, Player> mappedPlayers,
            Dictionary<CardModel, CardModel> existingCardMap,
            IEnumerable<CombatState> sourceStates)
        {
            TargetRun = targetRun;
            Players = mappedPlayers;
            Cards = existingCardMap;
            var pending = new Queue<CombatState>(sourceStates);
            while (pending.TryDequeue(out CombatState? source))
            {
                if (States.ContainsKey(source)) continue;
                if (source._engine?.IsInProgress == true)
                    throw new RunCloneNotSupportedException(RunCloneRejectionReason.ActiveCombat,
                        "An active combat cannot be part of a stable run clone.");
                if (source._engine?.IsStarting == true)
                    throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                        "Combat setup is still executing.");
                States.Add(source, source.NewCloneState(targetRun, projection: false));
                foreach (Creature creature in source.EnumerateCloneCreatures())
                    if (creature.CombatState is CombatState bound) pending.Enqueue(bound);
            }
            // Forward state references are now complete; no last-state-wins player binding.
            CloneGraph(targetRun, mappedPlayers, existingCardMap, new CloneStates(States), Creatures, Powers, projection: false);
            Map = new CombatCloneMap(existingCardMap, mappedPlayers, Creatures);
        }
    }

    internal CombatState CloneForRun(RunState targetRun,
        IReadOnlyDictionary<Player, Player> mappedPlayers,
        Dictionary<CardModel, CardModel> existingCardMap,
        out CombatCloneMap map, RunCloneContext context)
    {
        if (!ReferenceEquals(targetRun, context.TargetRun)
            || !ReferenceEquals(mappedPlayers, context.Players)
            || !ReferenceEquals(existingCardMap, context.Cards))
            throw new InvalidOperationException("Stable combat roots must use the same run clone context.");
        map = context.Map;
        return context.States[this];
    }

    private IEnumerable<Creature> EnumerateCloneCreatures() =>
        _allies.Concat(_enemies).Concat(_escapedCreatures).Concat(_removedCreatures);

    private CombatState NewCloneState(IRunState runState, bool projection) => new(runState, _encounterSlots)
    {
        CurrentSide = CurrentSide,
        RoundNumber = RoundNumber,
        IsPlayerExtraTurn = IsPlayerExtraTurn,
        GoldWasStolen = GoldWasStolen,
        _nextCreatureId = _nextCreatureId,
        _shuffleOrdinal = _shuffleOrdinal,
        _potionUseOrdinal = _potionUseOrdinal,
        _semanticCombatKey = _semanticCombatKey,
        CardSelectionSource = projection ? RejectingCardSelectionDecisionSource.Instance
            : ((RunState)runState).CardSelectionSource,
        IsProjection = projection || IsProjection,
    };

    private static void ClonePowerCardsFromList(
        List<Creature> creatures,
        IReadOnlyDictionary<Player, Player> playerMap,
        Dictionary<CardModel, CardModel> cardMap)
    {
        for (int i = 0; i < creatures.Count; i++)
        {
            List<PowerModel> powers = creatures[i].PowerList;
            for (int p = 0; p < powers.Count; p++)
            {
                foreach (CardModel card in powers[p].EnumerateCombatCloneCards())
                {
                    if (!cardMap.ContainsKey(card))
                    {
                        cardMap.Add(card, card.CloneForCombat(playerMap[card.Owner]));
                    }
                }
            }
        }
    }

    private static void CloneGraph(IRunState runState,
        IReadOnlyDictionary<Player, Player> playerMap,
        Dictionary<CardModel, CardModel> cardMap,
        CloneStates states,
        Dictionary<Creature, Creature> creatureMap,
        Dictionary<PowerModel, PowerModel>? powerMap, bool projection)
    {
        foreach ((CombatState sourceState, _) in states)
        {
            ClonePowerCardsFromList(sourceState._allies, playerMap, cardMap);
            ClonePowerCardsFromList(sourceState._enemies, playerMap, cardMap);
            ClonePowerCardsFromList(sourceState._escapedCreatures, playerMap, cardMap);
            ClonePowerCardsFromList(sourceState._removedCreatures, playerMap, cardMap);
        }

        Queue<CardModel>? origins = null;
        foreach (CardModel card in cardMap.Keys)
        {
            if (card.CloneOf is { } origin && !cardMap.ContainsKey(origin))
            {
                (origins ??= new Queue<CardModel>()).Enqueue(card);
            }
        }
        if (origins is not null)
        {
            while (origins.TryDequeue(out CardModel? card))
            {
                if (card.CloneOf is not { } origin || cardMap.ContainsKey(origin)) continue;
                cardMap.Add(origin, origin.CloneForCombat(playerMap[origin.Owner]));
                origins.Enqueue(origin);
            }
        }
        foreach ((CardModel source, CardModel target) in cardMap)
            target.RestoreCombatCloneReferencesFrom(source, cardMap);
        foreach ((Player source, Player target) in playerMap)
        {
            List<RelicModel> srcRelics = source.RelicList;
            List<RelicModel> dstRelics = target.RelicList;
            for (int i = 0; i < srcRelics.Count; i++)
                dstRelics[i].RestoreCombatCloneReferencesFrom(srcRelics[i], cardMap);
        }

        foreach ((CombatState source, CombatState target) in states)
        {
            CopyCreatures(source._allies, target._allies, target, false, false);
            CopyCreatures(source._enemies, target._enemies, target, false, false);
            CopyCreatures(source._escapedCreatures, target._escapedCreatures, target, true, false);
            CopyCreatures(source._removedCreatures, target._removedCreatures, target, false, true);
            if (source._spawnedEnemies.Count > 0)
            {
                target._spawnedEnemies.EnsureCapacity(source._spawnedEnemies.Count);
                for (int i = 0; i < source._spawnedEnemies.Count; i++)
                {
                    target._spawnedEnemies.Add(creatureMap[source._spawnedEnemies[i]]);
                }
            }
        }
        foreach ((Creature source, Creature target) in creatureMap)
        {
            if (source.PetOwner is { } sourceOwner)
            {
                Player clonedOwner = playerMap[sourceOwner];
                target.PetOwner = clonedOwner;
                if (sourceOwner.PlayerCombatState?.Pets.Contains(source) == true)
                    clonedOwner.PlayerCombatState!.AddPetInternal(target);
            }
            List<PowerModel> srcPowers = source.PowerList;
            for (int i = 0; i < srcPowers.Count; i++)
            {
                PowerModel sourcePower = srcPowers[i];
                if (powerMap?.ContainsKey(sourcePower) == true) continue;
                var clonedPower = (PowerModel)sourcePower.MutableClone();
                clonedPower.ApplyInternal(target, sourcePower.Amount);
                if (powerMap is null)
                    clonedPower.RestoreCombatCloneReferencesFrom(sourcePower, cardMap, creatureMap);
                else
                    powerMap.Add(sourcePower, clonedPower);
            }
        }
        // Every creature and power identity exists before reference and callback rebinding.
        if (powerMap is not null)
            foreach ((PowerModel source, PowerModel target) in powerMap)
                target.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        foreach ((Creature source, Creature target) in creatureMap)
            if (source.Monster is { } sourceMonster)
            {
                target.Monster!.RestoreCombatCloneReferencesFrom(sourceMonster, creatureMap);
                target.Monster!.RestoreCombatCloneMoveStateFrom(sourceMonster);
            }
        foreach ((CombatState source, CombatState target) in states)
        {
            if (projection)
            {
                IReadOnlyList<RunModel> clonedRunModels = source.RunModels.Count == 0 ? [] :
                    Array.AsReadOnly(source.RunModels.Select(model => model.CloneForRun(runState)).ToArray());
                target.RunModels = clonedRunModels;
                if (runState is CombatRunStateSnapshot snapshot)
                {
                    snapshot.SetRunModels(clonedRunModels);
                }
            }
            else
            {
                target.RunModels = runState.RunModels;
            }

            target.DamageHistory = source.DamageHistory.Clone(creatureMap, cardMap);
            target.SemanticHistory = source.SemanticHistory.Clone();
            source._engine?.CloneFor(target);
        }
        foreach ((Player source, Player target) in playerMap)
            creatureMap.TryAdd(source.Creature, target.Creature);

        void CopyCreatures(List<Creature> sources, List<Creature> targets,
            CombatState targetState, bool detachedInProjection, bool preserveDetachedInProjection)
        {
            foreach (Creature source in sources)
            {
                if (!creatureMap.TryGetValue(source, out Creature? target))
                {
                    CombatState? boundState = !projection
                        ? (source.CombatState is { } bound ? states[bound] : null)
                        : targetState;
                    target = CloneCreature(source, boundState ?? targetState, runState, playerMap);
                    if (!projection)
                        target.CombatState = boundState;
                    else if (detachedInProjection)
                        target.CombatState = null;
                    else if (preserveDetachedInProjection && source.CombatState is null)
                        target.CombatState = null;
                    creatureMap.Add(source, target);
                }
                targets.Add(target);
            }
        }
    }

    // Value-type singleton view keeps public combat search free of a new state-map allocation.
    private readonly struct CloneStates
    {
        private readonly CombatState? _source;
        private readonly CombatState? _target;
        private readonly Dictionary<CombatState, CombatState>? _many;
        internal CloneStates(CombatState source, CombatState target)
        {
            _source = source;
            _target = target;
            _many = null;
        }
        internal CloneStates(Dictionary<CombatState, CombatState> many)
        {
            _source = null;
            _target = null;
            _many = many;
        }
        internal CombatState this[ICombatState source] => _many is not null
            ? source is CombatState concrete ? _many[concrete]
                : throw new InvalidOperationException("A stable run root has a non-concrete combat state.")
            : ReferenceEquals(source, _source) ? _target!
                : throw new KeyNotFoundException("Combat state is outside the cloned world.");
        public Enumerator GetEnumerator() => new(_source, _target, _many);
        public struct Enumerator
        {
            private readonly CombatState? _source;
            private readonly CombatState? _target;
            private readonly bool _many;
            private bool _singlePending;
            private Dictionary<CombatState, CombatState>.Enumerator _items;
            internal Enumerator(CombatState? source, CombatState? target,
                Dictionary<CombatState, CombatState>? many)
            {
                _source = source;
                _target = target;
                _many = many is not null;
                _singlePending = !_many;
                _items = many is null ? default : many.GetEnumerator();
            }
            public KeyValuePair<CombatState, CombatState> Current => _many ? _items.Current
                : new(_source!, _target!);
            public bool MoveNext()
            {
                if (_many) return _items.MoveNext();
                if (!_singlePending) return false;
                _singlePending = false;
                return true;
            }
            public void Dispose() => _items.Dispose();
        }
    }

    private static Creature CloneCreature(
        Creature source,
        CombatState combatState,
        IRunState runState,
        IReadOnlyDictionary<Player, Player> playerMap)
    {
        Creature clone;
        if (source.Player is { } sourcePlayer)
        {
            clone = playerMap[sourcePlayer].Creature;
            clone.CombatId = source.CombatId;
            clone.CombatState = combatState;
            clone.AssignSlotName(source.SlotName);
        }
        else if (source.Monster is { } sourceMonster)
        {
            var clonedMonster = (MonsterModel)sourceMonster.MutableClone();
            clone = new Creature(clonedMonster, source.Side)
            {
                CombatId = source.CombatId,
                CombatState = combatState,
            };
            clone.AssignSlotName(source.SlotName);
            clonedMonster.PrepareCombatCloneStateFrom(sourceMonster, clone, runState.Rng);
        }
        else
        {
            throw new InvalidOperationException("Combat creatures must be backed by a player or monster model.");
        }

        clone.SetMaxHpInternal(source.MaxHp);
        clone.HealInternal(source.MaxHp);
        clone.LoseHpInternal(source.MaxHp - source.CurrentHp);
        clone.GainBlockInternal(source.Block);
        clone.RestoreCumulativeHpLostFrom(source);
        clone.IsAttacking = source.IsAttacking;
        return clone;
    }

    /// <summary>
    /// A combat-local run facade is sufficient for Beam Search: Task 2 never advances maps, rooms, rewards,
    /// or event sequences. Sharing the current room is deliberate because combat code only reads its identity/type;
    /// cloning the full <see cref="Runs.RunState"/> would duplicate unrelated mutable run machinery.
    /// </summary>
    private sealed class CombatRunStateSnapshot(IRunState source, RunRngSet rng) : IRunState
    {
        private readonly List<Player> _players = new();
        private readonly int _totalFloor = source.TotalFloor;
        private IReadOnlyList<RunModel> _runModels = Array.Empty<RunModel>();

        public GameMode GameMode { get; } = source.GameMode;

        public IReadOnlyList<RunModel> RunModels => _runModels;

        internal void SetRunModels(IReadOnlyList<RunModel> runModels) => _runModels = runModels;

        public MapLocation MapLocation { get; } = source switch
        {
            Runs.RunState concreteRun => concreteRun.MapLocation,
            CombatRunStateSnapshot snapshot => snapshot.MapLocation,
            _ => default,
        };

        // 由调用方给定：Clone 传 CloneExact 的副本，CloneForPublicWorld 传全新的集合。
        public RunRngSet Rng { get; } = rng;

        // AscensionManager contains only a readonly level, so it is immutable after construction.
        public AscensionManager Ascension { get; } = source.Ascension;

        public IReadOnlyList<Player> Players => _players;

        public int TotalFloor => _totalFloor;

        public AbstractRoom? CurrentRoom { get; } = source.CurrentRoom;

        public AbstractRoom? BaseRoom { get; } = source.BaseRoom;

        public void AddPlayer(Player player) => _players.Add(player);

        // Same order and capture points as RunState (#1072): one shared implementation, no second source walk.
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            Runs.RunState.IterateRunHookListeners(this, childCombatState);
    }
}
