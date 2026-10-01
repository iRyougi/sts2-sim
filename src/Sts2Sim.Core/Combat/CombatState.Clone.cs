using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
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
    /// 刻意<b>不</b>提供 <c>CloneReseeded</c> 对应物（Plan 08b-1 决定）：唯一的潜在消费者是
    /// 战略层 playout 的确定化采样，而它要到 08d 才存在。在没有真实消费者的情况下，
    /// 怪物私有 RNG、玩家 RNG、run RNG 三者各自该不该重播种、按什么种子重播种，无法正确决定。
    /// RngSet 层的能力已经就绪（<see cref="Runs.RunRngSet.CloneReseeded"/>），
    /// 届时在此之上组装即可，不需要再改接口层。
    /// </para>
    /// </remarks>
    public CombatState Clone() => Clone(out _);

    public CombatState Clone(out CombatCloneMap map)
    {
        var runState = new CombatRunStateSnapshot(RunState);
        var cardMap = new Dictionary<CardModel, CardModel>(ReferenceEqualityComparer.Instance);
        var playerMap = new Dictionary<Player, Player>(ReferenceEqualityComparer.Instance);
        foreach (Player player in Players)
        {
            Player clonedPlayer = player.CloneForCombat(runState, cardMap);
            playerMap.Add(player, clonedPlayer);
            runState.AddPlayer(clonedPlayer);
        }

        // Powers can retain cards that have left every pile (for example Dampen
        // remembers a played Power card). Add those roots before closing CloneOf
        // provenance and rebinding card references, preserving shared identities.
        foreach (Creature creature in _allies.Concat(_enemies)
                     .Concat(_escapedCreatures).Concat(_removedCreatures))
        {
            foreach (CardModel card in creature.Powers.SelectMany(power => power.EnumerateCombatCloneCards()))
            {
                if (!cardMap.ContainsKey(card))
                {
                    cardMap.Add(card, card.CloneForCombat(playerMap[card.Owner]));
                }
            }
        }

        // A clone can outlive its source's combat pile (for example a played Power card).
        // Include all reachable origins before rebinding, so projections never share mutable provenance.
        var origins = new Queue<CardModel>(cardMap.Keys);
        while (origins.TryDequeue(out CardModel? card))
        {
            if (card.CloneOf is not { } origin || cardMap.ContainsKey(origin)) continue;
            CardModel clonedOrigin = origin.CloneForCombat(playerMap[origin.Owner]);
            cardMap.Add(origin, clonedOrigin);
            origins.Enqueue(origin);
        }

        foreach ((CardModel source, CardModel target) in cardMap)
        {
            target.RestoreCombatCloneReferencesFrom(source, cardMap);
        }

        foreach ((Player source, Player target) in playerMap)
        {
            foreach ((RelicModel sourceRelic, RelicModel targetRelic) in
                     source.Relics.Zip(target.Relics))
            {
                targetRelic.RestoreCombatCloneReferencesFrom(sourceRelic, cardMap);
            }
        }

        var clone = new CombatState(runState, _encounterSlots)
        {
            CurrentSide = CurrentSide,
            RoundNumber = RoundNumber,
            IsPlayerExtraTurn = IsPlayerExtraTurn,
            GoldWasStolen = GoldWasStolen,
            _nextCreatureId = _nextCreatureId,
            _shuffleOrdinal = _shuffleOrdinal,
            _potionUseOrdinal = _potionUseOrdinal,
            _semanticCombatKey = _semanticCombatKey,
            CardSelectionSource = RejectingCardSelectionDecisionSource.Instance,
        };
        var creatureMap = new Dictionary<Creature, Creature>(ReferenceEqualityComparer.Instance);

        foreach (Creature ally in _allies)
        {
            Creature clonedAlly = CloneCreature(ally, clone, runState, playerMap);
            clone._allies.Add(clonedAlly);
            creatureMap.Add(ally, clonedAlly);
        }

        foreach (Creature enemy in _enemies)
        {
            Creature clonedEnemy = CloneCreature(enemy, clone, runState, playerMap);
            clone._enemies.Add(clonedEnemy);
            creatureMap.Add(enemy, clonedEnemy);
        }

        foreach (Creature escaped in _escapedCreatures)
        {
            Creature clonedEscaped = CloneCreature(escaped, clone, runState, playerMap);
            clonedEscaped.CombatState = null;
            clone._escapedCreatures.Add(clonedEscaped);
            creatureMap.Add(escaped, clonedEscaped);
        }

        foreach (Creature removed in _removedCreatures)
        {
            Creature clonedRemoved = CloneCreature(removed, clone, runState, playerMap);
            clonedRemoved.CombatState = removed.CombatState is null ? null : clone;
            clone._removedCreatures.Add(clonedRemoved);
            creatureMap.Add(removed, clonedRemoved);
        }

        // Rebind pet ownership only after the complete creature graph exists. A dead pet can retain
        // PetOwner after it has been removed from PlayerCombatState.Pets, so restore the two relations separately.
        foreach ((Creature source, Creature target) in creatureMap)
        {
            if (source.PetOwner is not { } sourceOwner)
            {
                continue;
            }

            Player clonedOwner = playerMap[sourceOwner];
            target.PetOwner = clonedOwner;
            if (sourceOwner.PlayerCombatState?.Pets.Contains(source) == true)
            {
                clonedOwner.PlayerCombatState!.AddPetInternal(target);
            }
        }

        clone._spawnedEnemies.AddRange(_spawnedEnemies.Select(creature => creatureMap[creature]));

        foreach ((Creature source, Creature target) in creatureMap)
        {
            foreach (PowerModel sourcePower in source.Powers)
            {
                var clonedPower = (PowerModel)sourcePower.MutableClone();
                clonedPower.ApplyInternal(target, sourcePower.Amount);
                clonedPower.RestoreCombatCloneReferencesFrom(sourcePower, cardMap, creatureMap);
            }
        }

        // Transient moves can bind callbacks to powers, so restore the move graph only after powers exist.
        foreach ((Creature source, Creature target) in creatureMap)
        {
            if (source.Monster is { } sourceMonster)
            {
                target.Monster!.RestoreCombatCloneMoveStateFrom(sourceMonster);
            }
        }

        clone.DamageHistory = DamageHistory.Clone(creatureMap, cardMap);
        clone.SemanticHistory = SemanticHistory.Clone();

        _engine?.CloneFor(clone);
        clone.IsProjection = true;
        foreach ((Player source, Player target) in playerMap)
        {
            creatureMap.TryAdd(source.Creature, target.Creature);
        }
        map = new CombatCloneMap(cardMap, playerMap, creatureMap);
        return clone;
    }

    private static Creature CloneCreature(
        Creature source,
        CombatState combatState,
        CombatRunStateSnapshot runState,
        IReadOnlyDictionary<Player, Player> playerMap)
    {
        Creature clone;
        if (source.Player is { } sourcePlayer)
        {
            clone = playerMap[sourcePlayer].Creature;
        }
        else if (source.Monster is { } sourceMonster)
        {
            var clonedMonster = (MonsterModel)sourceMonster.MutableClone();
            clone = new Creature(clonedMonster, source.Side);
            clonedMonster.PrepareCombatCloneStateFrom(sourceMonster, clone, runState.Rng);
        }
        else
        {
            throw new InvalidOperationException("Combat creatures must be backed by a player or monster model.");
        }

        clone.CombatId = source.CombatId;
        clone.CombatState = combatState;
        clone.AssignSlotName(source.SlotName);
        clone.SetMaxHpInternal(source.MaxHp);
        clone.HealInternal(source.MaxHp);
        clone.LoseHpInternal(source.MaxHp - source.CurrentHp, ValueProp.Unblockable);
        clone.GainBlockInternal(source.Block);
        clone.RestoreCumulativeHpLostFrom(source);
        return clone;
    }

    /// <summary>
    /// A combat-local run facade is sufficient for Beam Search: Task 2 never advances maps, rooms, rewards,
    /// or event sequences. Sharing the current room is deliberate because combat code only reads its identity/type;
    /// cloning the full <see cref="Runs.RunState"/> would duplicate unrelated mutable run machinery.
    /// </summary>
    private sealed class CombatRunStateSnapshot(IRunState source) : IRunState
    {
        private readonly List<Player> _players = new();
        private readonly int _totalFloor = source.TotalFloor;

        public MapLocation MapLocation { get; } = source switch
        {
            Runs.RunState concreteRun => concreteRun.MapLocation,
            CombatRunStateSnapshot snapshot => snapshot.MapLocation,
            _ => default,
        };

        public RunRngSet Rng { get; } = source.Rng.CloneExact();

        // AscensionManager contains only a readonly level, so it is immutable after construction.
        public AscensionManager Ascension { get; } = source.Ascension;

        public IReadOnlyList<Player> Players => _players;

        public int TotalFloor => _totalFloor;

        public AbstractRoom? CurrentRoom { get; } = source.CurrentRoom;

        public AbstractRoom? BaseRoom { get; } = source.BaseRoom;

        public void AddPlayer(Player player) => _players.Add(player);

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState)
        {
            foreach (Player player in _players)
            {
                foreach (RelicModel relic in player.Relics)
                {
                    if (!relic.IsMelted)
                        yield return relic;
                }

                foreach (PotionModel potion in player.PotionSlots.OfType<PotionModel>())
                {
                    yield return potion;
                }

                if (childCombatState is null)
                {
                    foreach (CardModel card in player.Deck.Cards)
                    {
                        yield return card;
                    }
                }
            }

            if (childCombatState is not null)
            {
                foreach (AbstractModel listener in childCombatState.IterateHookListeners())
                {
                    yield return listener;
                }
            }
        }
    }
}
