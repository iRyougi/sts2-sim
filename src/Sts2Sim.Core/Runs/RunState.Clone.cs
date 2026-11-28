using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Rewards;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Sts2Sim.Core.Runs;

public sealed partial class RunState
{
    /// <summary>True only for a parallel world whose hidden RNG and future have been replaced.</summary>
    public bool IsImaginationClone { get; private set; }
    private RunState(RunState source, RunRngSet rng, bool imagination = false)
    {
        Rng = rng;
        GameMode = source.GameMode;
        _acts = source._acts.Select(act => imagination
            ? act.CloneForVisibleHistory() : act.CloneForRun()).ToArray();
        _readOnlyActs = Array.AsReadOnly(_acts);
        CurrentActIndex = source.CurrentActIndex;
        EntryPhase = source.EntryPhase;
        Ascension = source.Ascension; // AscensionManager contains only an immutable level.
        Progress = source.Progress; // Immutable record.
        WongoPointsEarned = source.WongoPointsEarned;
        FreedRepy = source.FreedRepy;
        _completedQuests.AddRange(source._completedQuests);
        _visitedMapCoords.AddRange(source._visitedMapCoords);
        _visibleMapVisits.AddRange(source._visibleMapVisits);
        _unknownMapPointEntriesThisAct = source._unknownMapPointEntriesThisAct;
        _completedActFloors = source._completedActFloors;
        _selectingMapEventBeforeHistoryAppend = source._selectingMapEventBeforeHistoryAppend;
        _currentMapPointHasShop = source._currentMapPointHasShop;
        PreviousMapPointHasShop = source.PreviousMapPointHasShop;
        _eventsVisited = source._eventsVisited;
        _normalEncountersVisited = source._normalEncountersVisited;
        _eliteEncountersVisited = source._eliteEncountersVisited;
        _bossEncountersVisited = source._bossEncountersVisited;
        _nextRoomId = source._nextRoomId;
        _visitedEventIds.UnionWith(source._visitedEventIds);
        foreach (KeyValuePair<int, Type> visit in source._visitedAncientTypes)
            _visitedAncientTypes.Add(visit.Key, visit.Value);
        _hasVisibleAncientHistory = source._hasVisibleAncientHistory;
        _currentAncientEventType = source._currentAncientEventType;
        _bossEncounter = source._bossEncounter;
        _secondBossEncounter = source._secondBossEncounter;
        IsImaginationClone = source.IsImaginationClone;

        Odds = imagination
            ? RunOddsSet.FromVisibleHistory(Rng.UnknownMapPoint, new HookOddsAdapter(this),
                source.Odds.UnknownMapPoint.PublicResetBaseRules,
                source.Odds.UnknownMapPoint.PublicBaseRules, source._visibleMapVisits)
            : source.Odds.CloneExact(Rng.UnknownMapPoint, new HookOddsAdapter(this));
        SharedRelicGrabBag = imagination
            ? source.SharedRelicGrabBag?.CloneReshuffled(newRngForBag("shared"))
            : source.SharedRelicGrabBag?.Clone();
        int estimatedCards = 0;
        for (int i = 0; i < source._players.Count; i++)
        {
            Player p = source._players[i];
            estimatedCards += p.Deck.CardList.Count;
            if (p.PlayerCombatState is { } pcs)
            {
                estimatedCards += pcs.Hand.CardList.Count + pcs.DrawPile.CardList.Count
                    + pcs.DiscardPile.CardList.Count + pcs.ExhaustPile.CardList.Count + pcs.PlayPile.CardList.Count;
            }
        }
        var cardMap = new Dictionary<CardModel, CardModel>(estimatedCards + 8, ReferenceEqualityComparer.Instance);
        var playerMap = new Dictionary<Player, Player>(source._players.Count, ReferenceEqualityComparer.Instance);
        for (int i = 0; i < source._players.Count; i++)
        {
            Player player = source._players[i];
            Player copied = imagination ? player.CloneReseededForRun(this, cardMap)
                : player.CloneForRun(this, cardMap);
            _players.Add(copied);
            playerMap.Add(player, copied);
        }
        var modelMap = new Dictionary<AbstractModel, AbstractModel>(cardMap.Count + 16, ReferenceEqualityComparer.Instance);
        foreach (RunModel original in source._runModels)
        {
            RunModel copied = original.CloneForRun(this);
            _runModels.Add(copied);
            modelMap.Add(original, copied);
        }
        foreach ((CardModel original, CardModel copied) in cardMap) modelMap.Add(original, copied);
        foreach ((Player original, Player copied) in playerMap)
        {
            List<RelicModel> srcRelics = original.RelicList;
            List<RelicModel> dstRelics = copied.RelicList;
            for (int i = 0; i < srcRelics.Count; i++) modelMap.TryAdd(srcRelics[i], dstRelics[i]);
            List<PotionModel?> srcPotions = original.PotionSlotList;
            List<PotionModel?> dstPotions = copied.PotionSlotList;
            for (int i = 0; i < srcPotions.Count; i++)
                if (srcPotions[i] is { } pSrc) modelMap.TryAdd(pSrc, dstPotions[i]!);
        }
        CopyMapPointQuests(source.Map.StartingMapPoint, playerMap, cardMap, modelMap);
        CopyMapPointQuests(source.Map.BossMapPoint, playerMap, cardMap, modelMap);
        foreach (MapPoint point in source.Map.GetAllMapPoints())
            CopyMapPointQuests(point, playerMap, cardMap, modelMap);

        static void CopyMapPointQuests(
            MapPoint point,
            Dictionary<Player, Player> pMap,
            Dictionary<CardModel, CardModel> cMap,
            Dictionary<AbstractModel, AbstractModel> mMap)
        {
            IReadOnlyCollection<AbstractModel> quests = point.Quests;
            if (quests.Count == 0) return;
            foreach (AbstractModel original in quests)
            {
                if (original.IsCanonical || mMap.ContainsKey(original)) continue;
                AbstractModel copied = original is CardModel card && card.HasOwner
                    ? card.CloneForCombat(pMap[card.Owner]) : original.MutableClone();
                mMap.Add(original, copied);
                if (original is CardModel sourceCard) cMap.TryAdd(sourceCard, (CardModel)copied);
            }
        }
        Map = source.Map.CloneForRun(modelMap, imagination
            ? new Rng(rng.Seed, source.Map is SpoilsActMap or ActEntryMapState { Kind: nameof(SpoilsActMap) }
                ? "spoils_map" : $"act_{CurrentActIndex + 1}_map") : null);

        if (!imagination && source._generatedRooms is not null)
        {
            _generatedRooms = source._generatedRooms.Select(rooms => new GeneratedActRooms(
                rooms.Events.ToList(), rooms.NormalEncounters.ToList(), rooms.EliteEncounters.ToList(),
                rooms.Boss, rooms.SecondBoss, rooms.Ancient)).ToArray();
            GeneratedActRooms active = _generatedRooms[CurrentActIndex];
            _eventSequence = active.Events;
            _normalEncounterSequence = active.NormalEncounters;
            _eliteEncounterSequence = active.EliteEncounters;
        }
        else
        {
            _eventSequence = imagination ? null! : source._eventSequence?.ToList()!;
            _normalEncounterSequence = imagination ? null! : source._normalEncounterSequence?.ToList()!;
            _eliteEncounterSequence = imagination ? null! : source._eliteEncounterSequence?.ToList()!;
        }
        if (imagination)
        {
            IsImaginationClone = true;
            ResampleHiddenFutureForImagination(source);
        }
        CopyOwnedRunGraphFrom(source, playerMap, cardMap, modelMap, imagination);
        Rng newRngForBag(string owner) => rng.ForSemanticKey(
            Entities.Rngs.RunRngType.UpFront, $"imagination/relic_bag/{owner}");
        // Runtime decision sources are never copied. The caller attaches one to the clone.
    }

    /// <summary>
    /// Makes a parallel world from visible history. The root seed, every run/player RNG, hidden
    /// encounter and event order, future acts, and relic-bag order are replaced independently.
    /// Current map, current Boss, player inventory and public history remain unchanged. The
    /// strategic layer may consume only this world, never the live run's hidden future.
    /// </summary>
    public RunState CloneReseeded(ulong imaginationSeed)
    {
        AssertStableCloneBoundary();
        RequireVisibleUnknownMapPointHistory();
        if (!_hasVisibleAncientHistory)
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.MissingVisibleAncientHistory,
                "The imported run has no complete visible Ancient history; its hidden seed is not a substitute.");
        string seedText = $"run-clone-{imaginationSeed:X16}";
        RunRngSet newRng = Rng.UsesSemanticKeys ? RunRngSet.CreateKeyed(seedText) : new RunRngSet(seedText);
        var clone = new RunState(this, newRng, imagination: true);
        return clone;
    }

    /// <summary>Copies exact logical state at a stable run or room decision boundary.</summary>
    public RunState CloneExact()
    {
        AssertStableCloneBoundary();
        return new RunState(this, Rng.CloneExact());
    }

    /// <summary>Shared conditional continuation for between-room and in-room parallel worlds.</summary>
    private void ResampleHiddenFutureForImagination(RunState source)
    {
        RunRngSet newRng = Rng;

        // Capture the only known encounter history before replacing the hidden room sequence.
        EncounterDefinition[] visitedNormal = source._generatedRooms is null
            ? [] : source._normalEncounterSequence
                .Take(Math.Min(source._normalEncountersVisited, source._normalEncounterSequence.Count)).ToArray();
        EncounterDefinition[] visitedElite = source._generatedRooms is null
            ? [] : source._eliteEncounterSequence
                .Take(Math.Min(source._eliteEncountersVisited, source._eliteEncounterSequence.Count)).ToArray();
        int bossVisits = source._bossEncountersVisited;
        GeneratedActRooms[] regenerated = GenerateAllActRooms();
        if (source._generatedRooms is null)
        {
            _generatedRooms = regenerated;
            InitializeCurrentAct();
            return;
        }

        bool isBlitz = GameMode == GameMode.Blitz;
        ActDefinition current = Act;
        current.RebuildEncounterBagsFromHistory(visitedNormal, visitedElite, isBlitz);
        List<EncounterDefinition> normals = visitedNormal.ToList();
        for (int slot = normals.Count; slot < current.BaseNumberOfRooms; slot++)
            normals.Add(current.PickEncounter(RoomType.Monster,
                newRng.ForSemanticKey(Entities.Rngs.RunRngType.UpFront,
                    $"imagination/act={CurrentActIndex}/normal/slot={slot}"),
                isBlitz));
        List<EncounterDefinition> elites = visitedElite.ToList();
        for (int slot = elites.Count; slot < EliteEncounterPoolSize; slot++)
            elites.Add(current.PickEncounter(RoomType.Elite,
                newRng.ForSemanticKey(Entities.Rngs.RunRngType.UpFront,
                    $"imagination/act={CurrentActIndex}/elite/slot={slot}")));
        List<Type> remainingEvents = current.EffectiveEventPool
            .Where(type => !_visitedEventIds.Contains(((EventModel)ModelDb.Get(type)).Id))
            .ToList();
        remainingEvents.UnstableShuffle(newRng.ForSemanticKey(
            Entities.Rngs.RunRngType.UpFront,
            $"imagination/act={CurrentActIndex}/events"));
        GeneratedActRooms visibleCurrent = source._generatedRooms[CurrentActIndex];
        Type ancient = source._currentRooms.OfType<EventRoom>().Any(room =>
            room.Event.GetType() == visibleCurrent.Ancient)
            ? visibleCurrent.Ancient : regenerated[CurrentActIndex].Ancient;
        regenerated[CurrentActIndex] = new GeneratedActRooms(remainingEvents, normals, elites,
            visibleCurrent.Boss, visibleCurrent.SecondBoss, ancient);
        _generatedRooms = regenerated;
        InitializeCurrentAct();
        _normalEncountersVisited = visitedNormal.Length;
        _eliteEncountersVisited = visitedElite.Length;
        _bossEncountersVisited = bossVisits;
        _eventsVisited = 0;
    }

    private void AssertStableCloneBoundary()
    {
        if (_nextEncounterForTransplant is not null)
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingTransplant,
                "A transplant encounter is waiting to be entered.");
        if (Rng.HasOpenSemanticScope)
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.OpenSemanticScope,
                "A semantic RNG scope has not been closed.");
        for (int i = 0; i < _players.Count; i++)
        {
            if (_players[i].PlayerRng.HasOpenSemanticScope)
                throw new RunCloneNotSupportedException(RunCloneRejectionReason.OpenSemanticScope,
                    "A semantic RNG scope has not been closed.");
        }
        for (int i = 0; i < _currentRooms.Count; i++)
        {
            AbstractRoom room = _currentRooms[i];
            if (room is CombatRoom combat) combat.AssertRunCloneBoundary();
            if (room is EventRoom @event) @event.Event.AssertRunCloneBoundary();
        }
    }
}



public sealed partial class RunState
{
    // mapWorldRoot resolves existing identities or publishes/queues a world-root shell.
    // It must not eagerly restore callbacks; world roots finish before Complete/consumer execution.
    private sealed class RunCloneCallbackTargets(
        Dictionary<object, object> identities,
        Func<object, object?> mapWorldRoot)
    {
        private static readonly MethodInfo MemberwiseCopy = typeof(object).GetMethod(
            "MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, FieldInfo[]> CaptureFields = new();
        private readonly Queue<(object Source, object Target, string Path)> _pending = new();

        internal object Target(object source) => MapCapture(source, "callback.Target")!;

        internal Delegate Rebind(Delegate source)
        {
            if (identities.TryGetValue(source, out object? existing)) return (Delegate)existing;
            Delegate? result = null;
            foreach (Delegate invocation in Delegate.EnumerateInvocationList(source))
            {
                Delegate rebound = invocation.Target is null
                    ? invocation.Method.CreateDelegate(invocation.GetType())
                    : invocation.Method.CreateDelegate(invocation.GetType(), Target(invocation.Target));
                result = result is null ? rebound : Delegate.Combine(result, rebound);
            }
            // Target publishes only a shell. No callback is invoked until Complete has
            // remapped its fields, so a closure -> delegate -> same closure cycle terminates.
            identities.Add(source, result!);
            return result!;
        }

        internal void Complete()
        {
            while (_pending.TryDequeue(out var pending))
            {
                if (pending.Source is Array sourceArray)
                {
                    var targetArray = (Array)pending.Target;
                    var indexes = new int[sourceArray.Rank];
                    for (int dimension = 0; dimension < indexes.Length; dimension++)
                        indexes[dimension] = sourceArray.GetLowerBound(dimension);
                    for (int item = 0; item < sourceArray.Length; item++)
                    {
                        targetArray.SetValue(MapCapture(sourceArray.GetValue(indexes), pending.Path + "[]"), indexes);
                        for (int dimension = indexes.Length - 1; dimension >= 0; dimension--)
                        {
                            if (++indexes[dimension] <= sourceArray.GetUpperBound(dimension)) break;
                            indexes[dimension] = sourceArray.GetLowerBound(dimension);
                        }
                    }
                    continue;
                }
                foreach (FieldInfo field in Fields(pending.Source.GetType()))
                    field.SetValue(pending.Target, MapCapture(field.GetValue(pending.Source),
                        pending.Path + "." + field.Name));
            }
        }

        private object? MapCapture(object? source, string path)
        {
            if (source is null) return null;
            Type type = source.GetType();
            if (IsScalar(type) || source is Type or MemberInfo) return source;
            if (identities.TryGetValue(source, out object? mapped)) return mapped;
            if (source is Delegate callback) return Rebind(callback);
            if (source is Task || source is IAsyncStateMachine)
                throw new InvalidOperationException($"A run callback capture is an async/runtime handle, not a logical data root: {path} ({type.FullName}).");
            if (mapWorldRoot(source) is { } worldRoot)
            {
                if (!identities.TryGetValue(source, out mapped)) identities.Add(source, worldRoot);
                else if (!ReferenceEquals(mapped, worldRoot))
                    throw new InvalidOperationException($"Conflicting run-clone identity at {path} ({type.FullName}).");
                return worldRoot;
            }
            object target;
            if (source is Array array)
                target = array.Clone();
            else if (type.IsValueType)
            {
                // A boxed value is copied when assigned into a containing field/array.
                // Finish it synchronously; delaying its fields would leave old references in that copy.
                target = MemberwiseCopy.Invoke(source, null)!;
                identities.Add(source, target);
                foreach (FieldInfo field in Fields(type))
                    field.SetValue(target, MapCapture(field.GetValue(source), path + "." + field.Name));
                return target;
            }
            else if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                target = MemberwiseCopy.Invoke(source, null)!;
            else
                throw new InvalidOperationException($"Unmapped mutable run callback capture: {path} ({type.FullName}).");
            identities.Add(source, target);
            _pending.Enqueue((source, target, path));
            return target;
        }

        private static bool IsScalar(Type type) => type.IsPrimitive || type.IsEnum
            || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid)
            || type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(TimeSpan);

        private static FieldInfo[] Fields(Type type) => CaptureFields.GetOrAdd(type, static captureType =>
        {
            var fields = new List<FieldInfo>();
            for (Type? current = captureType; current is not null && current != typeof(object); current = current.BaseType)
                fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            return fields.ToArray();
        });
    }
}


// Typed run/player/room/offer/combat shells must be published in this same memo before Complete.
public sealed partial class RunState
{
    private sealed class RunCloneOwnedModelGraph
    {
        private static readonly MethodInfo Memberwise = typeof(object).GetMethod(
            "MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private readonly Dictionary<object, object> _memo;
        private readonly Func<object, object?> _mapTypedRoot;
        private readonly Func<Rng, Rng> _copyRng;
        private readonly Queue<(object Source, object Target)> _pending = new();
        private RunCloneCallbackTargets? _callbacks;

        internal RunCloneOwnedModelGraph(Dictionary<object, object> memo,
            Func<object, object?> mapTypedRoot, Func<Rng, Rng> copyRng)
        {
            _memo = memo;
            _mapTypedRoot = mapTypedRoot;
            _copyRng = copyRng;
        }

        internal void BindCallbacks(RunCloneCallbackTargets callbacks) => _callbacks = callbacks;

        internal bool HasPending => _pending.Count != 0;

        internal void PublishAllModels(IEnumerable<AbstractModel> models)
        {
            // MutableClone preserves model invariants and clears runtime event subscriptions.
            // Every identity is published before any private field or delegate is restored.
            foreach (AbstractModel source in models)
            {
                if (source.IsCanonical || _memo.ContainsKey(source)) continue;
                AbstractModel target = source.MutableClone();
                _memo.Add(source, target);
                _pending.Enqueue((source, target));
            }
        }

        internal object? MapOwnedRoot(object source)
        {
            if (_memo.TryGetValue(source, out object? mapped)) return mapped;
            if (source is AbstractModel model)
            {
                if (model.IsCanonical) return model;
                AbstractModel target = model.MutableClone();
                _memo.Add(source, target);
                _pending.Enqueue((source, target));
                return target;
            }
            if (_mapTypedRoot(source) is { } typed) return typed;
            Type type = source.GetType();
            if (source is Sts2Sim.Core.Random.Rng or Array || type.Assembly == typeof(RunState).Assembly
                || (type.Assembly == typeof(List<>).Assembly || type.Assembly == typeof(Stack<>).Assembly)
                    && type.Namespace == "System.Collections.Generic")
                return MapValue(source);
            return null;
        }

        internal void QueueExistingModelFields(AbstractModel source, AbstractModel target)
        {
            if (_memo.TryGetValue(source, out object? existing) && !ReferenceEquals(existing, target))
                throw new InvalidOperationException("Two target identities were published for one source model.");
            _memo[source] = target;
            _pending.Enqueue((source, target));
        }

        private static readonly ConcurrentDictionary<Type, Action<object, object, Func<object?, object?>, Func<object, object?>>> CompiledModelFieldCopiers = new();

        internal void Complete()
        {
            Func<object?, object?> mapVal = MapValue;
            while (_pending.TryDequeue(out var pair))
            {
                Type runtimeType = pair.Source.GetType();
                if (!runtimeType.IsValueType)
                {
                    var copier = CompiledModelFieldCopiers.GetOrAdd(runtimeType, BuildCompiledModelFieldCopier);
                    copier(pair.Source, pair.Target, mapVal, _mapTypedRoot);
                    continue;
                }

                foreach (FieldInfo field in RunCloneOwnedRootDiscovery.InstanceFields(runtimeType))
                {
                    if (RunCloneOwnedRootDiscovery.IsRuntimeSubscription(field)) continue;
                    if (RunCloneOwnedRootDiscovery.IsRuntimeBoundary(field)) continue;
                    if (field.DeclaringType == typeof(EventModel)) continue;
                    if (RunCloneOwnedRootDiscovery.IsCombatScopedCreatureReference(field))
                    {
                        object? creature = field.GetValue(pair.Source);
                        field.SetValue(pair.Target, creature is null ? null : _mapTypedRoot(creature));
                        continue;
                    }
                    field.SetValue(pair.Target, MapValue(field.GetValue(pair.Source)));
                }
            }
        }

        private static bool IsImmutableScalarValueType(Type fieldType)
        {
            if (fieldType.IsPrimitive || fieldType.IsEnum
                || fieldType == typeof(decimal) || fieldType == typeof(Guid)
                || fieldType == typeof(DateTime) || fieldType == typeof(DateTimeOffset)
                || fieldType == typeof(TimeSpan))
            {
                return true;
            }

            if (Nullable.GetUnderlyingType(fieldType) is { } underlying)
            {
                return underlying.IsPrimitive || underlying.IsEnum
                    || underlying == typeof(decimal) || underlying == typeof(Guid)
                    || underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)
                    || underlying == typeof(TimeSpan);
            }

            return false;
        }

        private static Action<object, object, Func<object?, object?>, Func<object, object?>> BuildCompiledModelFieldCopier(Type runtimeType)
        {
            ParameterExpression srcObj = Expression.Parameter(typeof(object), "srcObj");
            ParameterExpression dstObj = Expression.Parameter(typeof(object), "dstObj");
            ParameterExpression mapValParam = Expression.Parameter(typeof(Func<object?, object?>), "mapVal");
            ParameterExpression mapTypedParam = Expression.Parameter(typeof(Func<object, object?>), "mapTyped");

            ParameterExpression srcTyped = Expression.Variable(runtimeType, "src");
            ParameterExpression dstTyped = Expression.Variable(runtimeType, "dst");
            var body = new List<Expression>
            {
                Expression.Assign(srcTyped, Expression.Convert(srcObj, runtimeType)),
                Expression.Assign(dstTyped, Expression.Convert(dstObj, runtimeType)),
            };

            MethodInfo invokeMapVal = typeof(Func<object?, object?>).GetMethod("Invoke")!;
            MethodInfo invokeMapTyped = typeof(Func<object, object?>).GetMethod("Invoke")!;

            foreach (FieldInfo field in RunCloneOwnedRootDiscovery.InstanceFields(runtimeType))
            {
                if (RunCloneOwnedRootDiscovery.IsRuntimeSubscription(field)) continue;
                if (RunCloneOwnedRootDiscovery.IsRuntimeBoundary(field)) continue;
                if (field.DeclaringType == typeof(EventModel)) continue;

                // Readonly fields cannot be assigned via Expression.Assign without reflection fallback;
                // immutable scalar readonly fields are already copied identically by MemberwiseClone!
                Type ft = field.FieldType;
                bool isDirectScalar = IsImmutableScalarValueType(ft)
                    || ft == typeof(string) || ft == typeof(ModelId) || ft == typeof(Type);

                if (field.IsInitOnly)
                {
                    if (isDirectScalar) continue;
                    // Non-scalar readonly reference/struct field (e.g., private readonly List<T>): use reflection helper
                    ConstantExpression fConst = Expression.Constant(field);
                    if (RunCloneOwnedRootDiscovery.IsCombatScopedCreatureReference(field))
                    {
                        body.Add(Expression.Call(
                            typeof(RunCloneOwnedModelGraph).GetMethod(nameof(CopyReadonlyCombatCreatureField), BindingFlags.Static | BindingFlags.NonPublic)!,
                            fConst, srcObj, dstObj, mapTypedParam));
                    }
                    else
                    {
                        body.Add(Expression.Call(
                            typeof(RunCloneOwnedModelGraph).GetMethod(nameof(CopyReadonlyField), BindingFlags.Static | BindingFlags.NonPublic)!,
                            fConst, srcObj, dstObj, mapValParam));
                    }
                    continue;
                }

                MemberExpression srcField = Expression.Field(srcTyped, field);
                MemberExpression dstField = Expression.Field(dstTyped, field);

                if (RunCloneOwnedRootDiscovery.IsCombatScopedCreatureReference(field))
                {
                    Expression mappedCreature = Expression.Condition(
                        Expression.ReferenceEqual(srcField, Expression.Constant(null, ft)),
                        Expression.Constant(null, ft),
                        Expression.Convert(Expression.Call(mapTypedParam, invokeMapTyped, Expression.Convert(srcField, typeof(object))), ft));
                    body.Add(Expression.Assign(dstField, mappedCreature));
                }
                else if (isDirectScalar)
                {
                    body.Add(Expression.Assign(dstField, srcField));
                }
                else
                {
                    Expression mapped = Expression.Convert(
                        Expression.Call(mapValParam, invokeMapVal, Expression.Convert(srcField, typeof(object))),
                        ft);
                    body.Add(Expression.Assign(dstField, mapped));
                }
            }

            return Expression.Lambda<Action<object, object, Func<object?, object?>, Func<object, object?>>>(
                Expression.Block([srcTyped, dstTyped], body),
                srcObj, dstObj, mapValParam, mapTypedParam).Compile();
        }

        private static void CopyReadonlyField(FieldInfo field, object source, object target, Func<object?, object?> mapVal) =>
            field.SetValue(target, mapVal(field.GetValue(source)));

        private static void CopyReadonlyCombatCreatureField(FieldInfo field, object source, object target, Func<object, object?> mapTyped)
        {
            object? creature = field.GetValue(source);
            field.SetValue(target, creature is null ? null : mapTyped(creature));
        }

        private object? MapValue(object? source)
        {
            if (source is null) return null;
            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || source is string or Type or MemberInfo
                or decimal or Guid or DateTime or DateTimeOffset or TimeSpan) return source;
            // ModelId has only constructor-set string fields. Preserve its canonical
            // identity instead of allocating a second identifier for an owned model.
            if (type == typeof(ModelId)) return source;
            // Zero-length arrays have no mutable slots; sharing preserves the framework's
            // Array.Empty identity when later cards are created from canonical templates.
            if (source is Array { Length: 0 }) return source;
            if (_memo.TryGetValue(source, out object? mapped)) return mapped;
            if (source is AbstractModel model && model.IsCanonical) return model;
            if (source is Rng rng)
            {
                Rng target = _copyRng(rng);
                _memo.Add(source, target);
                return target;
            }
            if (source is Delegate callback)
                return (_callbacks ?? throw new InvalidOperationException("Callback mapper was not bound.")).Rebind(callback);
            if (source is Task || source is IAsyncStateMachine)
                throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                    $"Runtime callback handle cannot be copied: {type.FullName}.");
            if (source is AbstractModel) return MapOwnedRoot(source);
            if (_mapTypedRoot(source) is { } typed) return typed;
            if (source is RunState or Player or Creature or CombatState or AbstractRoom or RewardsSet or MerchantInventory)
                throw new InvalidOperationException($"Typed world root was not published: {type.FullName}.");
            if (source is StringComparer || ReferenceEquals(source, ReferenceEqualityComparer.Instance)
                || IsRuntimeDefaultComparer(type)) return source;
            // Reinsert keys/elements: cloned object identities have different hashes.
            // Copying Dictionary/HashSet buckets would retain source hash codes.
            if (source is IDictionary dictionary && type.IsGenericType)
            {
                object? comparer = type.GetProperty("Comparer")!.GetValue(source);
                object target = type.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                    ? Activator.CreateInstance(type, dictionary.Count, comparer)!
                    : Activator.CreateInstance(type, comparer)!;
                _memo.Add(source, target);
                foreach (DictionaryEntry entry in dictionary)
                    ((IDictionary)target).Add(MapValue(entry.Key)!, MapValue(entry.Value));
                return target;
            }
            if (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(HashSet<>)
                || type.GetGenericTypeDefinition() == typeof(SortedSet<>)))
            {
                object? comparer = type.GetProperty("Comparer")!.GetValue(source);
                int count = (int)type.GetProperty("Count")!.GetValue(source)!;
                object target = type.GetGenericTypeDefinition() == typeof(HashSet<>)
                    ? Activator.CreateInstance(type, count, comparer)!
                    : Activator.CreateInstance(type, comparer)!;
                _memo.Add(source, target);
                MethodInfo add = type.GetMethod("Add", [type.GetGenericArguments()[0]])!;
                foreach (object? item in (IEnumerable)source)
                    add.Invoke(target, [MapValue(item)]);
                return target;
            }
            if (source is Array array)
            {
                Array target = (Array)array.Clone();
                _memo.Add(source, target);
                Type elementType = type.GetElementType()!;
                if (array.Length == 0 || elementType.IsPrimitive || elementType.IsEnum
                    || elementType == typeof(string)) return target;
                int[] indices = Enumerable.Range(0, array.Rank).Select(array.GetLowerBound).ToArray();
                while (true)
                {
                    target.SetValue(MapValue(array.GetValue(indices)), indices);
                    int dimension = array.Rank - 1;
                    while (dimension >= 0 && indices[dimension] == array.GetUpperBound(dimension))
                    {
                        indices[dimension] = array.GetLowerBound(dimension);
                        dimension--;
                    }
                    if (dimension < 0) break;
                    indices[dimension]++;
                }
                return target;
            }
            bool dataContainer = (type.Assembly == typeof(List<>).Assembly
                || type.Assembly == typeof(Stack<>).Assembly)
                && type.Namespace == "System.Collections.Generic";
            if (type.Assembly != typeof(RunState).Assembly && !type.IsValueType && !dataContainer
                && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                throw new InvalidOperationException($"Unmapped mutable model data: {type.FullName}.");
            object shell = Memberwise.Invoke(source, null)!;
            _memo.Add(source, shell);
            if (type.IsValueType)
            {
                // Boxed structs must be fully mapped before a field/array copies their value.
                foreach (FieldInfo field in RunCloneOwnedRootDiscovery.InstanceFields(type))
                    field.SetValue(shell, MapValue(field.GetValue(source)));
            }
            else _pending.Enqueue((source, shell));
            return shell;
        }

        private static bool IsRuntimeDefaultComparer(Type type) =>
            type.Assembly == typeof(EqualityComparer<>).Assembly
            && type.Namespace == "System.Collections.Generic"
            && type.GetInterfaces().Any(contract => contract.IsGenericType
                && (contract.GetGenericTypeDefinition() == typeof(IEqualityComparer<>)
                    || contract.GetGenericTypeDefinition() == typeof(IComparer<>)));
    }
}


// Unapplied nested component for the existing Runs/RunState.Clone.cs destination.
public sealed partial class RunState
{
    private static class RunCloneOwnedRootDiscovery
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> Fields = new();
        private static readonly ConcurrentDictionary<Type, Action<object, Queue<object>>?> CompiledChildEnqueuers = new();
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> PrivateRngFields = new();

        internal static FieldInfo[] RngFields(Type type) => PrivateRngFields.GetOrAdd(type, static t =>
            InstanceFields(t).Where(static f => f.FieldType == typeof(Rng)).ToArray());

        private static bool CannotContainModelRoots(Type fieldType)
        {
            if (fieldType.IsPrimitive || fieldType.IsEnum
                || fieldType == typeof(string) || fieldType == typeof(decimal)
                || fieldType == typeof(ModelId) || fieldType == typeof(Type)
                || fieldType == typeof(MemberInfo) || fieldType == typeof(Rng)
                || fieldType == typeof(Guid) || fieldType == typeof(DateTime)
                || fieldType == typeof(DateTimeOffset) || fieldType == typeof(TimeSpan))
            {
                return true;
            }

            if (Nullable.GetUnderlyingType(fieldType) is { } underlying)
            {
                return CannotContainModelRoots(underlying);
            }

            return false;
        }

        private static Action<object, Queue<object>>? BuildChildEnqueuer(Type runtimeType)
        {
            if (runtimeType.IsValueType)
            {
                FieldInfo[] traversable = InstanceFields(runtimeType)
                    .Where(static f => !IsRuntimeSubscription(f) && !IsRuntimeBoundary(f)
                        && !IsCombatScopedCreatureReference(f) && !CannotContainModelRoots(f.FieldType))
                    .ToArray();
                if (traversable.Length == 0) return null;
                return (obj, q) =>
                {
                    for (int i = 0; i < traversable.Length; i++)
                    {
                        if (traversable[i].GetValue(obj) is { } val) q.Enqueue(val);
                    }
                };
            }

            ParameterExpression objParam = Expression.Parameter(typeof(object), "obj");
            ParameterExpression queueParam = Expression.Parameter(typeof(Queue<object>), "queue");
            ParameterExpression typedVar = Expression.Variable(runtimeType, "typed");
            MethodInfo enqueueMethod = typeof(Queue<object>).GetMethod("Enqueue", [typeof(object)])!;

            var statements = new List<Expression>
            {
                Expression.Assign(typedVar, Expression.Convert(objParam, runtimeType)),
            };
            int count = 0;
            foreach (FieldInfo field in InstanceFields(runtimeType))
            {
                if (IsRuntimeSubscription(field) || IsRuntimeBoundary(field)
                    || IsCombatScopedCreatureReference(field) || CannotContainModelRoots(field.FieldType))
                {
                    continue;
                }
                count++;
                MemberExpression fieldExpr = Expression.Field(typedVar, field);
                if (!field.FieldType.IsValueType)
                {
                    ParameterExpression valVar = Expression.Variable(typeof(object), "v");
                    statements.Add(Expression.Block(
                        [valVar],
                        Expression.Assign(valVar, Expression.Convert(fieldExpr, typeof(object))),
                        Expression.IfThen(
                            Expression.ReferenceNotEqual(valVar, Expression.Constant(null, typeof(object))),
                            Expression.Call(queueParam, enqueueMethod, valVar))));
                }
                else
                {
                    ParameterExpression boxedVar = Expression.Variable(typeof(object), "b");
                    statements.Add(Expression.Block(
                        [boxedVar],
                        Expression.Assign(boxedVar, Expression.Convert(fieldExpr, typeof(object))),
                        Expression.IfThen(
                            Expression.ReferenceNotEqual(boxedVar, Expression.Constant(null, typeof(object))),
                            Expression.Call(queueParam, enqueueMethod, boxedVar))));
                }
            }

            if (count == 0) return null;
            return Expression.Lambda<Action<object, Queue<object>>>(
                Expression.Block([typedVar], statements),
                objParam, queueParam).Compile();
        }

        internal static IReadOnlyList<AbstractModel> Collect(IEnumerable<object> roots,
            Func<object, bool>? skipRoot = null)
        {
            var models = new List<AbstractModel>(64);
            var seen = new HashSet<object>(128, ReferenceEqualityComparer.Instance);
            var pending = new Queue<object>(64);
            foreach (object root in roots) pending.Enqueue(root);
            while (pending.TryDequeue(out object? current))
            {
                if (!seen.Add(current) || skipRoot?.Invoke(current) == true) continue;
                Type type = current.GetType();
                if (type.IsPrimitive || type.IsEnum || current is string or Type or MemberInfo
                    or decimal or Guid or DateTime or DateTimeOffset or TimeSpan or Sts2Sim.Core.Random.Rng) continue;
                if (current is AbstractModel model)
                {
                    if (model.IsCanonical) continue;
                    models.Add(model);
                    if (model is EventModel @event) @event.AssertRunCloneBoundary();
                }
                if (current is Task task)
                {
                    if (!task.IsCompletedSuccessfully)
                        throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                            "An owned graph root still has an unfinished or failed callback task.");
                    // Completed runtime handles are not model roots and are not copied.
                    continue;
                }
                if (current is Delegate callback)
                {
                    foreach (Delegate invocation in Delegate.EnumerateInvocationList(callback))
                        if (invocation.Target is { } target) pending.Enqueue(target);
                    continue;
                }
                if (current is IEnumerable items)
                {
                    foreach (object? item in items)
                        if (item is not null) pending.Enqueue(item);
                    continue;
                }
                if (type.Assembly != typeof(RunState).Assembly && !type.IsValueType
                    && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) continue;
                Action<object, Queue<object>>? enqueuer = CompiledChildEnqueuers.GetOrAdd(type, BuildChildEnqueuer);
                enqueuer?.Invoke(current, pending);
            }
            return models;
        }

        internal static bool IsRuntimeSubscription(FieldInfo field) =>
            field.DeclaringType == typeof(AbstractModel) && field.Name == "ExecutionFinished"
            || field.DeclaringType == typeof(RelicModel) && field.Name == "Flashed";

        internal static bool IsRuntimeBoundary(FieldInfo field) =>
            field.Name is "_stateLock" or "_lifecycleLock" or "_rewardLock" or "_takeLock" or "_resolutionLock"
                or "_purchaseGate" or "_revealGate" or "_interactionGate" or "_insideLifecycle" or "_observer"
                or "_cardSelectionSource" or "<CardSelectionSource>k__BackingField"
            || field.DeclaringType == typeof(CombatState) && field.Name is "_hookListenerSnapshotCache" or "_hookListenerLists"
            || field.DeclaringType == typeof(EventModel)
                && field.Name is "_activeChoiceTask" or "_activeChoiceOption" or "_activeChoicePageVersion";

        /// <summary>模型上指向"本场战斗生物"的字段（原版战后不清空）。跑局克隆不下钻、只映射已发布的生物，
        /// 未发布（战后残留）映射为 null（#974）。</summary>
        internal static bool IsCombatScopedCreatureReference(FieldInfo field) =>
            field.DeclaringType == typeof(Sts2Sim.Core.Models.Relics.Wuppy) && field.Name == "_combatWuppy";

        internal static FieldInfo[] InstanceFields(Type type) => Fields.GetOrAdd(type, static rootType =>
        {
            var fields = new List<FieldInfo>();
            for (Type? current = rootType; current is not null && current != typeof(object); current = current.BaseType)
                fields.AddRange(current.GetFields(BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
            return fields.ToArray();
        });
    }
}


public sealed partial class RunState
{
    private static void PublishTypedPlayerAndModelRoots(
        IReadOnlyDictionary<Player, Player> players,
        Dictionary<CardModel, CardModel> cards,
        Dictionary<object, object> identities,
        RunCloneOwnedModelGraph models,
        IEnumerable<AbstractModel> ownedModels)
    {
        foreach ((Player source, Player target) in players)
        {
            Register(source, target);
            Register(source.Creature, target.Creature);
            Register(source.PlayerRng, target.PlayerRng);
            Register(source.Odds, target.Odds);
            Register(source.RelicGrabBag, target.RelicGrabBag);
            source.PlayerRng.RegisterNamedStreams(target.PlayerRng, Register);
            Register(source.Deck, target.Deck);
            if (source.PlayerCombatState is { } srcPcs && target.PlayerCombatState is { } dstPcs)
            {
                IReadOnlyList<CardPile> srcPiles = srcPcs.AllPiles;
                IReadOnlyList<CardPile> dstPiles = dstPcs.AllPiles;
                for (int i = 0; i < srcPiles.Count; i++) Register(srcPiles[i], dstPiles[i]);
            }
            List<RelicModel> srcRelics = source.RelicList;
            List<RelicModel> dstRelics = target.RelicList;
            for (int i = 0; i < srcRelics.Count; i++) RegisterModel(srcRelics[i], dstRelics[i]);
            List<PotionModel?> srcPotions = source.PotionSlotList;
            List<PotionModel?> dstPotions = target.PotionSlotList;
            for (int i = 0; i < srcPotions.Count; i++)
            {
                PotionModel? pFirst = srcPotions[i];
                PotionModel? pSecond = dstPotions[i];
                if (pFirst is not null && pSecond is not null) RegisterModel(pFirst, pSecond);
                else if ((pFirst is null) != (pSecond is null))
                    throw new InvalidOperationException("Potion-slot shape changed during player cloning.");
            }
        }
        // Include private held cards, provenance ancestors and closure-only cards before
        // CombatState builds its shared context. No new per-state private-card map is made.
        foreach (AbstractModel ownedModel in ownedModels)
        {
            if (ownedModel is not CardModel source || source.IsCanonical) continue;
            if (!cards.TryGetValue(source, out CardModel? target))
            {
                target = source.HasOwner
                    ? source.CloneForCombat(players[source.Owner])
                    : (CardModel)source.MutableClone();
                cards.Add(source, target);
            }
            RegisterModel(source, target);
        }

        void Register(object source, object target)
        {
            if (identities.TryGetValue(source, out object? existing))
            {
                if (!ReferenceEquals(existing, target))
                    throw new InvalidOperationException("A typed player/model root has two target identities.");
            }
            else identities.Add(source, target);
        }

        void RegisterModel(AbstractModel source, AbstractModel target)
        {
            Register(source, target);
            models.QueueExistingModelFields(source, target);
        }
    }
}


public sealed partial class RunState
{
    private sealed class RunCloneRewardLogicalGraph(
        Dictionary<object, object> identities,
        Func<object?, object?> mapValue)
    {
        private readonly Queue<(object Source, object Target)> _pending = new();
        internal bool HasPending => _pending.Count != 0;

        internal Reward MapReward(Reward source)
        {
            if (identities.TryGetValue(source, out object? existing)) return (Reward)existing;
            AssertStableReward(source);
            Reward target = (Reward)RuntimeHelpers.GetUninitializedObject(source.GetType());
            identities.Add(source, target);
            _pending.Enqueue((source, target));
            return target;
        }

        internal object? MapDataRoot(object source)
        {
            if (source is not (RewardsSet or MerchantInventory or MerchantEntry or
                RestSiteDecision or CardCreationOptions or CardRewardAlternative)) return null;
            if (identities.TryGetValue(source, out object? existing)) return existing;
            object target = RuntimeHelpers.GetUninitializedObject(source.GetType());
            identities.Add(source, target);
            _pending.Enqueue((source, target));
            return target;
        }

        internal object? MapReadonlyWrapper(object source)
        {
            Type type = source.GetType();
            if (!type.IsGenericType) return null;
            Type definition = type.GetGenericTypeDefinition();
            if (definition != typeof(ReadOnlyCollection<>) &&
                definition != typeof(ReadOnlyDictionary<,>)) return null;
            if (identities.TryGetValue(source, out object? existing)) return existing;
            object target = RuntimeHelpers.GetUninitializedObject(type);
            identities.Add(source, target);
            _pending.Enqueue((source, target));
            return target;
        }

        private readonly Dictionary<CardReward, CardReward> _clonedCardRewards = new(ReferenceEqualityComparer.Instance);

        internal void Complete()
        {
            while (_pending.TryDequeue(out var pair))
            {
                foreach (FieldInfo field in RunCloneOwnedRootDiscovery.InstanceFields(pair.Source.GetType()))
                {
                    object? value = field.GetValue(pair.Source);
                    if (IsRewardLock(field) || field.Name == "_syncRoot")
                    {
                        // Locks are runtime controls, never a reference to the live branch.
                        object? gate = value is null ? null : IndependentGate(value);
                        field.SetValue(pair.Target, gate);
                    }
                    else if (IsRewardCompletion(field))
                    {
                        field.SetValue(pair.Target, value is null ? null : IndependentCompletion((Task)value));
                    }
                    else
                    {
                        // Includes Player, SelectedOption, _resolutionIdentity and wrapper backing
                        // collections. All must use the same memo as cards, callbacks and rooms.
                        field.SetValue(pair.Target, mapValue(value));
                    }
                }
                if (pair.Source is CardReward sourceCard) _clonedCardRewards.Add(sourceCard, (CardReward)pair.Target);
            }
        }

        /// <summary>Player.RelicObtained is never copied: cloned players start without subscribers (#1118). Called
        /// once after every Drain batch, so rewards mapped in a later batch keep the source players' invocation
        /// order and WingCharm draws Rewards RNG in the same order in both worlds.</summary>
        internal void RestoreRelicObtainedSubscriptions()
        {
            foreach (Player sourcePlayer in _clonedCardRewards.Keys.Select(card => card.Player).Distinct())
                foreach (CardReward sourceCard in sourcePlayer.RelicObtainedSubscribers().OfType<CardReward>())
                    if (_clonedCardRewards.TryGetValue(sourceCard, out CardReward? target))
                        target.RestoreRelicObtainedSubscriptionAfterClone();
            _clonedCardRewards.Clear();
        }

        private object IndependentGate(object source)
        {
            if (identities.TryGetValue(source, out object? existing)) return existing;
            object target = new();
            identities.Add(source, target);
            return target;
        }

        private Task IndependentCompletion(Task source)
        {
            if (!source.IsCompletedSuccessfully)
                throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                    "A reward completion is pending, failed or cancelled; it cannot be copied as success.");
            if (identities.TryGetValue(source, out object? existing)) return (Task)existing;
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            completion.SetResult();
            identities.Add(source, completion.Task);
            return completion.Task;
        }

        private static void AssertStableReward(Reward source)
        {
            foreach (FieldInfo field in RunCloneOwnedRootDiscovery.InstanceFields(source.GetType()))
            {
                if (!IsRewardCompletion(field)) continue;
                if (field.GetValue(source) is Task completion &&
                    (!completion.IsCompletedSuccessfully || !source.IsResolved))
                    throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                        "Reward side effects have not completed at this decision boundary.");
            }
        }

        private static bool IsRewardLock(FieldInfo field) =>
            field.DeclaringType == typeof(TakeableReward) && field.Name == "_takeLock" ||
            field.DeclaringType == typeof(CardReward) && field.Name == "_resolutionLock";

        private static bool IsRewardCompletion(FieldInfo field) =>
            field.DeclaringType == typeof(TakeableReward) && field.Name == "_takeTask" ||
            field.DeclaringType == typeof(CardReward) && field.Name == "_resolutionTask";

    }
}


// The enclosing clone constructor must publish players/cards/model roots and create exactly
// one RunCloneContext before obtaining this mapper; no per-room context is constructed here.
public sealed partial class RunState
{
    private Func<AbstractRoom, AbstractRoom> CreateStableRoomMapper(
        CombatState.RunCloneContext context,
        Dictionary<object, object> identities,
        RunCloneOwnedModelGraph models,
        RunCloneCallbackTargets callbacks,
        Func<EventModel, EventModel> mapEvent,
        Func<Reward, Reward> mapReward,
        Func<RewardsSet, RewardsSet> mapOffer,
        Func<MerchantInventory, MerchantInventory> mapInventory,
        Func<RestSiteDecision, RestSiteDecision> mapDecision,
        bool imagination)
    {
        if (!ReferenceEquals(context.TargetRun, this))
            throw new InvalidOperationException("Stable rooms belong to another target run.");
        foreach ((CombatState source, CombatState target) in context.States)
            Register(source, target);
        foreach (var pair in context.Creatures)
        {
            Register(pair.Key, pair.Value);
            if (pair.Key.Monster is { } originalMonster && pair.Value.Monster is { } targetMonster)
            {
                Register(originalMonster, targetMonster);
                models.QueueExistingModelFields(originalMonster, targetMonster);
            }
        }
        foreach (var pair in context.Powers)
        {
            Register(pair.Key, pair.Value);
            models.QueueExistingModelFields(pair.Key, pair.Value);
        }
        return MapRoom;

        void Register(object source, object target)
        {
            if (identities.TryGetValue(source, out object? existing))
            {
                if (!ReferenceEquals(existing, target))
                    throw new InvalidOperationException("A stable world root has two clone identities.");
                return;
            }
            identities.Add(source, target);
        }

        Player MapPlayer(Player source) => context.Players[source];

        CombatState MapState(CombatState source) => source.CloneForRun(
            this, context.Players, context.Cards, out _, context);

        AbstractRoom MapRoom(AbstractRoom source)
        {
            if (identities.TryGetValue(source, out object? existing)) return (AbstractRoom)existing;
            return source switch
            {
                CombatRoom combat => combat.CloneForRun(this, MapPlayer, mapReward, mapOffer,
                    MapState, callbacks.Rebind, (original, copied) => Register(original, copied)),
                EventRoom @event => @event.CloneForRun(this, imagination, mapEvent,
                    combat => (CombatRoom)MapRoom(combat), callbacks.Rebind, Register),
                MerchantRoom merchant => merchant.CloneForRun(this, MapPlayer, mapInventory, mapOffer,
                    (original, copied) => Register(original, copied)),
                RestSiteRoom rest => rest.CloneForRun(MapPlayer, mapDecision, mapOffer,
                    (original, copied) => Register(original, copied)),
                TreasureRoom treasure => MapTreasure(treasure),
                _ => throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                    $"No approved stable-room copier exists for {source.GetType().FullName}.")
            };
        }

        TreasureRoom MapTreasure(TreasureRoom source)
        {
            TreasureRoom target = source.CloneForRun(MapPlayer);
            Register(source, target);
            return target;
        }
    }

    private void CopyStableRoomStackAndOffersFrom(RunState source,
        Func<AbstractRoom, AbstractRoom> mapRoom,
        Func<RewardsSet, RewardsSet> mapOffer)
    {
        foreach (AbstractRoom room in source._currentRooms) _currentRooms.Add(mapRoom(room));
        foreach (RewardsSet offer in source._activeRewardOffers) _activeRewardOffers.Add(mapOffer(offer));
        ForcedCombatResumeOutcome = source.ForcedCombatResumeOutcome;
    }
}


public sealed partial class RunState
{
    private static readonly FieldInfo EventPreparedCombatRoomField = typeof(EventRoom).GetField(
        "_preparedCombatRoom", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo EventOwnerField = typeof(EventModel).GetField(
        "_owner", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo EventRngField = typeof(EventModel).GetField(
        "_rng", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo EventStateLockField = typeof(EventModel).GetField(
        "_stateLock", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo EventActiveChoiceTaskField = typeof(EventModel).GetField(
        "_activeChoiceTask", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo SphereOwnerField = typeof(CrystalSphereMinigame).GetField(
        "_owner", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly FieldInfo SphereRngField = typeof(CrystalSphereMinigame).GetField(
        "_rng", BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static readonly ConcurrentDictionary<Type, FieldInfo[]> RuntimeBoundaryModelFields = new();

    private void CopyOwnedRunGraphFrom(RunState source, Dictionary<Player, Player> players,
        Dictionary<CardModel, CardModel> cards,
        IReadOnlyDictionary<AbstractModel, AbstractModel> initialModels, bool imagination)
    {
        var identities = new Dictionary<object, object>(cards.Count + 128, ReferenceEqualityComparer.Instance)
        {
            [source] = this,
            [source.Rng] = Rng,
            [source.Odds] = Odds,
            [source.Map] = Map,
            [source._players] = _players,
            [source._runModels] = _runModels,
            [source._acts] = _acts,
            [source._readOnlyActs] = _readOnlyActs,
            [source._currentRooms] = _currentRooms,
            [source._activeRewardOffers] = _activeRewardOffers,
        };
        source.Rng.RegisterNamedStreams(Rng, (srcRng, dstRng) => identities.Add(srcRng, dstRng));
        if (source.SharedRelicGrabBag is not null && SharedRelicGrabBag is not null)
            identities.Add(source.SharedRelicGrabBag, SharedRelicGrabBag);
        for (int index = 0; index < _acts.Length; index++) identities.Add(source._acts[index], _acts[index]);
        PublishMapGraph(source.Map, Map);

        var hiddenPreparedStates = new HashSet<CombatState>(ReferenceEqualityComparer.Instance);
        if (imagination)
            for (int i = 0; i < source._currentRooms.Count; i++)
                if (source._currentRooms[i] is EventRoom room && PreparedRoom(room) is { } prepared)
                    foreach (CombatState state in prepared.EnumerateRunCloneStates())
                        hiddenPreparedStates.Add(state);
        IReadOnlyList<AbstractModel> owned = RunCloneOwnedRootDiscovery.Collect(Roots(),
            hiddenPreparedStates.Count == 0 ? null : root => root is CombatState state && hiddenPreparedStates.Contains(state));
        var privateRngs = new Dictionary<Rng, string>(ReferenceEqualityComparer.Instance);
        for (int m = 0; m < owned.Count; m++)
        {
            AbstractModel model = owned[m];
            FieldInfo[] rngFields = RunCloneOwnedRootDiscovery.RngFields(model.GetType());
            for (int f = 0; f < rngFields.Length; f++)
            {
                FieldInfo field = rngFields[f];
                if (field.GetValue(model) is not Rng stream) continue;
                string key = $"run-clone/model={model.Id}/field={field.DeclaringType!.FullName}.{field.Name}";
                privateRngs.TryAdd(stream, key);
            }
        }
        CombatState.RunCloneContext? context = null;
        Func<AbstractRoom, AbstractRoom>? roomMapper = null;
        var restoredEvents = new HashSet<EventModel>(ReferenceEqualityComparer.Instance);
        RunCloneOwnedModelGraph models = null!;
        RunCloneCallbackTargets callbacks = null!;
        RunCloneRewardLogicalGraph data = null!;
        models = new RunCloneOwnedModelGraph(identities, MapTypedRoot, CopyPrivateRng);
        callbacks = new RunCloneCallbackTargets(identities, MapCallbackRoot);
        data = new RunCloneRewardLogicalGraph(identities, MapLogicalValue);
        models.BindCallbacks(callbacks);
        PublishTypedPlayerAndModelRoots(players, cards, identities, models, owned);
        foreach ((Player original, Player target) in players)
            if (original.PlayerCombatState is { } combat && target.PlayerCombatState is { } copy)
                identities.Add(combat, copy);
        foreach ((AbstractModel original, AbstractModel target) in initialModels)
            models.QueueExistingModelFields(original, target);

        // All private cards and provenance roots exist before the one cross-state context.
        context = new CombatState.RunCloneContext(this, players, cards, StableCombatStates());
        roomMapper = CreateStableRoomMapper(context, identities, models, callbacks,
            MapEvent, data.MapReward, MapOffer, MapInventory, MapDecision, imagination);
        models.PublishAllModels(owned);
        foreach (EventModel model in owned.OfType<EventModel>()) MapEvent(model);
        RebuildModelControls();
        CopyStableRoomStackAndOffersFrom(source, roomMapper, MapOffer);
        Drain();
        data.RestoreRelicObtainedSubscriptions();

        IEnumerable<CombatState> StableCombatStates()
        {
            foreach (AbstractRoom room in source._currentRooms)
            {
                CombatRoom? combatRoom = room as CombatRoom;
                if (room is EventRoom @event)
                {
                    if (imagination) continue;
                    combatRoom = PreparedRoom(@event);
                }
                if (combatRoom is null) continue;
                foreach (CombatState state in combatRoom.EnumerateRunCloneStates()) yield return state;
            }
            foreach (Player player in source._players)
                if (player.Creature.CombatState is CombatState state && !hiddenPreparedStates.Contains(state))
                    yield return state;
        }

        static CombatRoom? PreparedRoom(EventRoom room) =>
            (CombatRoom?)EventPreparedCombatRoomField.GetValue(room);

        IEnumerable<object> Roots()
        {
            foreach (Player player in source._players) yield return player;
            foreach (AbstractRoom room in source._currentRooms) yield return room;
            foreach (RewardsSet offer in source._activeRewardOffers) yield return offer;
            foreach (var point in source.Map.GetAllMapPoints())
                foreach (AbstractModel quest in point.Quests) yield return quest;
        }

        object? MapTypedRoot(object original)
        {
            if (identities.TryGetValue(original, out object? mapped)) return mapped;
            if (original.GetType() == typeof(object))
            {
                object marker = new();
                identities.Add(original, marker);
                return marker;
            }
            if (original is Reward reward) return data.MapReward(reward);
            if (data.MapReadonlyWrapper(original) is { } wrapper) return wrapper;
            if (data.MapDataRoot(original) is { } root) return root;
            if (original is AbstractRoom room)
                return (roomMapper ?? throw new InvalidOperationException("Room mapping started before world roots."))(room);
            if (original is CombatState combat)
                return (context ?? throw new InvalidOperationException("Combat context was not constructed.")).States[combat];
            if (original is CrystalSphereMinigame game) return MapSphere(game);
            return null;
        }

        object? MapCallbackRoot(object original) => MapTypedRoot(original) ?? models.MapOwnedRoot(original);

        object? MapLogicalValue(object? value)
        {
            if (value is null) return null;
            Type type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || value is string or Type or MemberInfo or decimal
                or Guid or DateTime or DateTimeOffset or TimeSpan) return value;
            if (value is Delegate callback) return callbacks.Rebind(callback);
            return MapCallbackRoot(value) ?? callbacks.Target(value);
        }

        Rng CopyPrivateRng(Rng stream)
        {
            if (!imagination)
                return stream.CloneExact(observer => (Action<int, string, string>)callbacks.Rebind(observer));
            if (!privateRngs.TryGetValue(stream, out string? key))
                throw new InvalidOperationException("Private RNG has no logical owner field in this run graph.");
            return new Rng(Rng.Seed, key);
        }

        EventModel MapEvent(EventModel original)
        {
            EventModel target = (EventModel)models.MapOwnedRoot(original)!;
            if (!restoredEvents.Add(original)) return target;
            Player? owner = (Player?)EventOwnerField.GetValue(original);
            Rng? originalRng = (Rng?)EventRngField.GetValue(original);
            Rng eventRng = originalRng is null ? new Rng(Rng.Seed, $"run-clone/event={original.Id}")
                : (Rng)MapLogicalValue(originalRng)!;
            target.RestoreRunCloneStateFrom(original, owner is null ? null! : players[owner], this,
                eventRng, MapOffer, callbacks.Rebind);
            identities.TryAdd(EventStateLockField.GetValue(original)!, EventStateLockField.GetValue(target)!);
            if (EventActiveChoiceTaskField.GetValue(original) is Task done)
            {
                if (!done.IsCompletedSuccessfully)
                    throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                        "A previous event choice has not completed successfully.");
                if (!identities.TryGetValue(done, out object? independentCompletion))
                {
                    var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    completion.SetResult();
                    identities.Add(done, independentCompletion = completion.Task);
                }
                EventActiveChoiceTaskField.SetValue(target, independentCompletion);
            }
            // Route logical containers through the memo too: the same queue/option can be
            // reached through a world field and a callback capture.
            foreach (FieldInfo field in RunCloneOwnedRootDiscovery.InstanceFields(typeof(EventModel)))
            {
                if (field.DeclaringType != typeof(EventModel) ||
                    field.Name is "_stateLock" or "_owner" or "_runState" or "_rng" or "_activeChoiceTask"
                        or "_activeChoiceOption" or "_activeChoicePageVersion") continue;
                field.SetValue(target, MapLogicalValue(field.GetValue(original)));
            }
            return target;
        }

        RewardsSet MapOffer(RewardsSet original) => (RewardsSet)data.MapDataRoot(original)!;
        MerchantInventory MapInventory(MerchantInventory original) => (MerchantInventory)data.MapDataRoot(original)!;
        RestSiteDecision MapDecision(RestSiteDecision original) => (RestSiteDecision)data.MapDataRoot(original)!;

        CrystalSphereMinigame MapSphere(CrystalSphereMinigame original)
        {
            Player srcOwner = (Player)SphereOwnerField.GetValue(original)!;
            Player owner = players[srcOwner];
            Rng originalRng = (Rng)SphereRngField.GetValue(original)!;
            Rng rng;
            if (identities.TryGetValue(originalRng, out object? existingRng)) rng = (Rng)existingRng;
            else
            {
                rng = imagination
                    ? new Rng(Rng.Seed, $"run-clone/sphere/player={source._players.IndexOf(srcOwner)}")
                    : CopyPrivateRng(originalRng);
                identities.Add(originalRng, rng);
            }
            CrystalSphereItem MapItem(CrystalSphereItem item) => (CrystalSphereItem)MapLogicalValue(item)!;
            void RegisterGame(CrystalSphereMinigame a, CrystalSphereMinigame b) => identities.Add(a, b);
            void RegisterItem(CrystalSphereItem a, CrystalSphereItem b) => identities.Add(a, b);
            return imagination
                ? original.CloneReseededForRun(owner, rng, MapItem, data.MapReward, RegisterGame, RegisterItem)
                : original.CloneExactForRun(owner, rng, MapItem, data.MapReward, RegisterGame);
        }

        void Drain()
        {
            do
            {
                data.Complete();
                models.Complete();
                callbacks.Complete();
            } while (data.HasPending || models.HasPending);
        }

        void RebuildModelControls()
        {
            for (int m = 0; m < owned.Count; m++)
            {
                AbstractModel original = owned[m];
                FieldInfo[] boundaryFields = RuntimeBoundaryModelFields.GetOrAdd(original.GetType(), static t =>
                    RunCloneOwnedRootDiscovery.InstanceFields(t)
                        .Where(static f => RunCloneOwnedRootDiscovery.IsRuntimeBoundary(f) && f.DeclaringType != typeof(EventModel))
                        .ToArray());
                if (boundaryFields.Length == 0) continue;
                AbstractModel target = (AbstractModel)identities[original];
                for (int f = 0; f < boundaryFields.Length; f++)
                {
                    FieldInfo field = boundaryFields[f];
                    if (field.GetValue(original) is SemaphoreSlim gate)
                    {
                        if (gate.CurrentCount != 1)
                            throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                                "A custom event interaction is still executing.");
                        if (!identities.TryGetValue(gate, out object? independent))
                            identities.Add(gate, independent = new SemaphoreSlim(1, 1));
                        field.SetValue(target, independent);
                    }
                    else if (field.FieldType == typeof(object) && field.GetValue(original) is { } sourceGate)
                    {
                        if (!identities.TryGetValue(sourceGate, out object? independent))
                            identities.Add(sourceGate, independent = new object());
                        field.SetValue(target, independent);
                    }
                }
            }
        }

        void PublishMapGraph(Sts2Sim.Core.Map.ActMap original, Sts2Sim.Core.Map.ActMap target)
        {
            var pending = new Queue<(Sts2Sim.Core.Map.MapPoint Source, Sts2Sim.Core.Map.MapPoint Target)>(64);
            pending.Enqueue((original.StartingMapPoint, target.StartingMapPoint));
            pending.Enqueue((original.BossMapPoint, target.BossMapPoint));
            if (original.SecondBossMapPoint is { } second) pending.Enqueue((second, target.SecondBossMapPoint!));
            using (IEnumerator<Sts2Sim.Core.Map.MapPoint> srcEnum = original.GetAllMapPoints().GetEnumerator())
            using (IEnumerator<Sts2Sim.Core.Map.MapPoint> dstEnum = target.GetAllMapPoints().GetEnumerator())
            {
                while (srcEnum.MoveNext() && dstEnum.MoveNext())
                {
                    pending.Enqueue((srcEnum.Current, dstEnum.Current));
                }
            }
            while (pending.TryDequeue(out var pair))
            {
                if (identities.TryGetValue(pair.Source, out object? existing))
                {
                    if (!ReferenceEquals(existing, pair.Target))
                        throw new InvalidOperationException("Map edges disagree on node identity.");
                    continue;
                }
                identities.Add(pair.Source, pair.Target);
                using (IEnumerator<Sts2Sim.Core.Map.MapPoint> srcC = pair.Source.Children.GetEnumerator())
                using (IEnumerator<Sts2Sim.Core.Map.MapPoint> dstC = pair.Target.Children.GetEnumerator())
                {
                    while (srcC.MoveNext() && dstC.MoveNext()) pending.Enqueue((srcC.Current, dstC.Current));
                }
                using (IEnumerator<Sts2Sim.Core.Map.MapPoint> srcP = pair.Source.parents.GetEnumerator())
                using (IEnumerator<Sts2Sim.Core.Map.MapPoint> dstP = pair.Target.parents.GetEnumerator())
                {
                    while (srcP.MoveNext() && dstP.MoveNext()) pending.Enqueue((srcP.Current, dstP.Current));
                }
            }
        }
    }
}

