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

        CombatState clone = NewCloneState(runState, projection: true);
        var states = new CloneStates(this, clone);
        var creatures = new Dictionary<Creature, Creature>(ReferenceEqualityComparer.Instance);
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

    private static void CloneGraph(IRunState runState,
        IReadOnlyDictionary<Player, Player> playerMap,
        Dictionary<CardModel, CardModel> cardMap,
        CloneStates states,
        Dictionary<Creature, Creature> creatureMap,
        Dictionary<PowerModel, PowerModel>? powerMap, bool projection)
    {
        foreach ((CombatState sourceState, _) in states)
        {
            foreach (Creature creature in sourceState.EnumerateCloneCreatures())
            {
                foreach (CardModel card in creature.Powers.SelectMany(power => power.EnumerateCombatCloneCards()))
                    if (!cardMap.ContainsKey(card))
                        cardMap.Add(card, card.CloneForCombat(playerMap[card.Owner]));
            }
        }
        var origins = new Queue<CardModel>(cardMap.Keys);
        while (origins.TryDequeue(out CardModel? card))
        {
            if (card.CloneOf is not { } origin || cardMap.ContainsKey(origin)) continue;
            cardMap.Add(origin, origin.CloneForCombat(playerMap[origin.Owner]));
            origins.Enqueue(origin);
        }
        foreach ((CardModel source, CardModel target) in cardMap)
            target.RestoreCombatCloneReferencesFrom(source, cardMap);
        foreach ((Player source, Player target) in playerMap)
            foreach ((RelicModel sourceRelic, RelicModel targetRelic) in source.Relics.Zip(target.Relics))
                targetRelic.RestoreCombatCloneReferencesFrom(sourceRelic, cardMap);

        foreach ((CombatState source, CombatState target) in states)
        {
            CopyCreatures(source._allies, target._allies, target, false, false);
            CopyCreatures(source._enemies, target._enemies, target, false, false);
            CopyCreatures(source._escapedCreatures, target._escapedCreatures, target, true, false);
            CopyCreatures(source._removedCreatures, target._removedCreatures, target, false, true);
            target._spawnedEnemies.AddRange(source._spawnedEnemies.Select(creature => creatureMap[creature]));
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
            foreach (PowerModel sourcePower in source.Powers)
            {
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
                    target = CloneCreature(source, targetState, runState, playerMap);
                    if (!projection)
                        target.CombatState = source.CombatState is { } bound ? states[bound] : null;
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
