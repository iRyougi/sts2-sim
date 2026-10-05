using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
#if HOOK_LISTENER_CACHE_DIAGNOSTICS
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
#endif

namespace Sts2Sim.Core.Combat;

public sealed partial class CombatState
{
    // Derived data only. Every constructor/clone starts cold; no cache is copied.
    // All cache-owned references, versions and arrays live behind this single field.
    private HookListenerSnapshot? _hookListenerSnapshotCache;

    internal enum HookListenerPhase : byte { Energy, EnergyLate, Star, AfterPiles, AfterPilesLate }

    internal HookListenerSnapshot? GetHookListenerSnapshot(IRunState runState, HookListenerPhase phase)
    {
        // These sealed implementations have only pure field getters in the source scan.
        // Unknown interfaces and the no-child path keep their original enumerators.
        if (runState is not Runs.RunState and not CombatRunStateSnapshot)
            return null;

#if HOOK_LISTENER_CACHE_DIAGNOSTICS
        // A zero-allocation shape preflight keeps malformed sources on their old phase.
        // Materialize the independent OLD path before looking up/building the candidate.
        var oraclePreflight = default(ListenerWitnessScan);
        if (!ScanListenerSources(runState, ref oraclePreflight)) return null;
        AbstractModel[] oracleOld = runState.IterateHookListeners(this).ToArray();
#endif
        HookListenerSnapshot? snapshot = _hookListenerSnapshotCache;
#if HOOK_LISTENER_CACHE_DIAGNOSTICS
        long validationStart = Stopwatch.GetTimestamp();
#endif
        bool hit = snapshot is not null && snapshot.Matches(this, runState);
#if HOOK_LISTENER_CACHE_DIAGNOSTICS
        long validationTicks = snapshot is null ? 0 : Stopwatch.GetTimestamp() - validationStart;
        long captureStart = Stopwatch.GetTimestamp();
#endif
        if (!hit)
        {
            var scan = new ListenerWitnessScan(new List<object?>(256));
            if (!ScanListenerSources(runState, ref scan))
                return null; // Preserve malformed/null-listener exception timing in the old phase.
            AbstractModel[] full = runState.IterateHookListeners(this).ToArray();
            if (Array.Exists(full, static model => model is null))
                return null;
            snapshot = new HookListenerSnapshot(scan.Captured(), full, (snapshot?.Version ?? 0) + 1);
            _hookListenerSnapshotCache = snapshot;
        }
#if HOOK_LISTENER_CACHE_DIAGNOSTICS
        long captureTicks = hit ? 0 : Stopwatch.GetTimestamp() - captureStart;
        ListenerCacheDiagnostics.Compare(this, runState, oracleOld, snapshot!, phase, hit, validationTicks, captureTicks);
#endif
        return snapshot;
    }

    internal sealed class HookListenerSnapshot
    {
        internal readonly object?[] _witness;
        internal readonly AbstractModel[] Full;
        private readonly AbstractModel[][] _costListeners;
        private readonly byte[] _pileCoverage;
        internal long Version { get; }

        internal HookListenerSnapshot(object?[] witness, AbstractModel[] full, long version)
        {
            _witness = witness;
            Full = full;
            Version = version;
            var energy = new List<AbstractModel>();
            var late = new List<AbstractModel>();
            var star = new List<AbstractModel>();
            _pileCoverage = new byte[full.Length];
            for (int i = 0; i < full.Length; i++)
            {
                AbstractModel model = full[i];
                if (HookOverrideIndex.MayOverride(model, HookOverrideIndex.CostSlot.Energy)) energy.Add(model);
                if (HookOverrideIndex.MayOverride(model, HookOverrideIndex.CostSlot.EnergyLate)) late.Add(model);
                if (HookOverrideIndex.MayOverride(model, HookOverrideIndex.CostSlot.Star)) star.Add(model);
                if (HookOverrideIndex.MayOverride(model, HookOverrideIndex.PileSlot.Normal))
                    _pileCoverage[i] |= (byte)HookOverrideIndex.PileSlot.Normal;
                if (HookOverrideIndex.MayOverride(model, HookOverrideIndex.PileSlot.Late))
                    _pileCoverage[i] |= (byte)HookOverrideIndex.PileSlot.Late;
            }
            _costListeners = [energy.ToArray(), late.ToArray(), star.ToArray()];
        }

        internal bool Matches(CombatState state, IRunState runState)
        {
            var scan = new ListenerWitnessScan(_witness);
            return state.ScanListenerSources(runState, ref scan) && scan.Complete;
        }

        internal AbstractModel[] CostListeners(HookOverrideIndex.CostSlot slot) => slot switch
        {
            HookOverrideIndex.CostSlot.Energy => _costListeners[0],
            HookOverrideIndex.CostSlot.EnergyLate => _costListeners[1],
            HookOverrideIndex.CostSlot.Star => _costListeners[2],
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };

        internal bool OverridesPile(int index, HookOverrideIndex.PileSlot slot) =>
            (_pileCoverage[index] & (byte)slot) != 0;
    }

    // A flat, exact source witness: containers, positions, lengths and branch scalars.
    // Validation allocates nothing; counts/flags are boxed only when capturing a miss.
    // No List._version, collection hashes, content IDs or command notifications are used.
    private struct ListenerWitnessScan
    {
        private readonly List<object?>? _capture;
        private readonly object?[]? _expected;
        private int _position;
        internal ListenerWitnessScan(List<object?> capture) { _capture = capture; _expected = null; _position = 0; }
        internal ListenerWitnessScan(object?[] expected) { _expected = expected; _capture = null; _position = 0; }
        internal bool Complete => _position == _expected!.Length;
        internal object?[] Captured() => _capture!.ToArray();
        internal bool Reference(object? value)
        {
#if HOOK_LISTENER_CACHE_DIAGNOSTICS
            if (_capture is null && _expected is null) return true; // Shape-only oracle preflight.
#endif
            if (_capture is not null) { _capture.Add(value); return true; }
            return _position < _expected!.Length && ReferenceEquals(_expected[_position++], value);
        }
        internal bool Count(int value)
        {
#if HOOK_LISTENER_CACHE_DIAGNOSTICS
            if (_capture is null && _expected is null) return true; // Shape-only oracle preflight.
#endif
            if (_capture is not null) { _capture.Add(value); return true; }
            return _position < _expected!.Length && _expected[_position++] is int previous && previous == value;
        }
        internal bool Flag(bool value)
        {
#if HOOK_LISTENER_CACHE_DIAGNOSTICS
            if (_capture is null && _expected is null) return true; // Shape-only oracle preflight.
#endif
            if (_capture is not null) { _capture.Add(value); return true; }
            return _position < _expected!.Length && _expected[_position++] is bool previous && previous == value;
        }
    }

    private bool ScanListenerSources(IRunState runState, ref ListenerWitnessScan scan)
    {
        if (!scan.Reference(runState)) return false;
        IReadOnlyList<Player> players = runState.Players;
        if (players is null || !scan.Reference(players) || !scan.Count(players.Count)) return false;
        for (int i = 0; i < players.Count; i++)
        {
            Player player = players[i];
            if (player is null || !scan.Reference(player)) return false;
            IReadOnlyList<RelicModel> relics = player.Relics;
            if (relics is null || !scan.Reference(relics) || !scan.Count(relics.Count)) return false;
            for (int r = 0; r < relics.Count; r++)
            {
                RelicModel relic = relics[r];
                if (relic is null || !scan.Reference(relic) || !scan.Flag(relic.IsMelted)) return false;
            }
            IReadOnlyList<PotionModel?> potions = player.PotionSlots;
            if (potions is null || !scan.Reference(potions) || !scan.Count(potions.Count)) return false;
            for (int p = 0; p < potions.Count; p++)
                if (!scan.Reference(potions[p])) return false;
        }
        return ScanCreatureSources(_allies, ref scan) && ScanCreatureSources(_enemies, ref scan);
    }

    private static bool ScanCreatureSources(IReadOnlyList<Creature> creatures, ref ListenerWitnessScan scan)
    {
        if (creatures is null || !scan.Reference(creatures) || !scan.Count(creatures.Count)) return false;
        for (int i = 0; i < creatures.Count; i++)
        {
            Creature creature = creatures[i];
            if (creature is null || !scan.Reference(creature)) return false;
            IReadOnlyList<PowerModel> powers = creature.Powers;
            if (powers is null || !scan.Reference(powers) || !scan.Count(powers.Count)) return false;
            for (int p = 0; p < powers.Count; p++)
                if (powers[p] is null || !scan.Reference(powers[p])) return false;
            if (!scan.Reference(creature.Monster) || !scan.Reference(creature.Player)) return false;
            if (creature.IsMonster || creature.Player is not { } player) continue;
            if (!scan.Flag(player.IsActiveForHooks)) return false;
            if (!player.IsActiveForHooks) continue;
            PlayerCombatState? state = player.PlayerCombatState;
            if (!scan.Reference(state)) return false;
            if (state is null) continue;
            if (state.OrbQueue is not { } queue || !scan.Reference(queue)) return false;
            IReadOnlyList<OrbModel> orbs = queue.Orbs;
            if (orbs is null || !scan.Reference(orbs) || !scan.Count(orbs.Count)) return false;
            for (int o = 0; o < orbs.Count; o++)
                if (orbs[o] is null || !scan.Reference(orbs[o])) return false;
            if (!ScanPileSources(state.Hand, ref scan) || !ScanPileSources(state.DrawPile, ref scan)
                || !ScanPileSources(state.DiscardPile, ref scan) || !ScanPileSources(state.ExhaustPile, ref scan)
                || !ScanPileSources(state.PlayPile, ref scan)) return false;
        }
        return true;
    }

    private static bool ScanPileSources(CardPile pile, ref ListenerWitnessScan scan)
    {
        if (pile is null || !scan.Reference(pile)) return false;
        IReadOnlyList<CardModel> cards = pile.Cards;
        if (cards is null || !scan.Reference(cards) || !scan.Count(cards.Count)) return false;
        for (int c = 0; c < cards.Count; c++)
        {
            CardModel card = cards[c];
            if (card is null || !scan.Reference(card)) return false;
            IReadOnlyList<EnchantmentModel> enchantments = card.Enchantments;
            if (enchantments is null || !scan.Reference(enchantments) || !scan.Count(enchantments.Count)) return false;
            for (int e = 0; e < enchantments.Count; e++)
                if (enchantments[e] is null || !scan.Reference(enchantments[e])) return false;
            if (!scan.Reference(card.Affliction)) return false;
        }
        return true;
    }

#if HOOK_LISTENER_CACHE_DIAGNOSTICS
    private static class ListenerCacheDiagnostics
    {
        private delegate bool CostHook(CardModel card, decimal originalCost, out decimal modifiedCost);
        private delegate Task PileHook(CardModel card, PileType oldPileType, AbstractModel? clonedBy);
        private static readonly ConcurrentDictionary<(Type, HookListenerPhase), bool> OverrideOracle = new();
        private static readonly long[] Hits = new long[5], Misses = new long[5], ValidationTicks = new long[5], CaptureTicks = new long[5];

        static ListenerCacheDiagnostics() => AppDomain.CurrentDomain.ProcessExit += (_, _) =>
            Console.Error.WriteLine("HOOK_LISTENER_CACHE_DIAGNOSTICS " + JsonSerializer.Serialize(new
            {
                stopwatchFrequency = Stopwatch.Frequency,
                phases = Enum.GetValues<HookListenerPhase>().Select(phase => new
                {
                    phase = phase.ToString(), hits = Hits[(int)phase], misses = Misses[(int)phase],
                    validationTicks = ValidationTicks[(int)phase], captureAndBuildTicks = CaptureTicks[(int)phase],
                }).ToArray(),
            }));

        internal static void Compare(CombatState state, IRunState root, AbstractModel[] old, HookListenerSnapshot snapshot,
            HookListenerPhase phase, bool hit, long validationTicks, long captureTicks)
        {
            // Independent full oracle uses the untouched actual wrapper/eager implementation.
            // The independent slot oracle binds the base MethodInfo, never GetBaseDefinition.
            int first = 0;
            while (first < Math.Min(old.Length, snapshot.Full.Length)
                && ReferenceEquals(old[first], snapshot.Full[first])) first++;
            if (first != old.Length || first != snapshot.Full.Length)
                Fail("full", first);
            if (phase <= HookListenerPhase.Star)
            {
                var slot = phase switch
                {
                    HookListenerPhase.Energy => HookOverrideIndex.CostSlot.Energy,
                    HookListenerPhase.EnergyLate => HookOverrideIndex.CostSlot.EnergyLate,
                    _ => HookOverrideIndex.CostSlot.Star,
                };
                AbstractModel[] filtered = snapshot.CostListeners(slot);
                int position = 0;
                for (int i = 0; i < old.Length; i++)
                {
                    if (!Overrides(old[i], phase)) continue;
                    if (position >= filtered.Length || !ReferenceEquals(old[i], filtered[position]))
                        Fail("filtered", i);
                    position++;
                }
                if (position != filtered.Length) Fail("filtered-length", position);
            }
            else
            {
                HookOverrideIndex.PileSlot slot = phase == HookListenerPhase.AfterPiles
                    ? HookOverrideIndex.PileSlot.Normal : HookOverrideIndex.PileSlot.Late;
                for (int i = 0; i < old.Length; i++)
                    if (Overrides(old[i], phase) != snapshot.OverridesPile(i, slot))
                        Fail("pile-slot-and-finished-order", i);
            }
            int index = (int)phase;
            if (hit) Interlocked.Increment(ref Hits[index]); else Interlocked.Increment(ref Misses[index]);
            Interlocked.Add(ref ValidationTicks[index], validationTicks);
            Interlocked.Add(ref CaptureTicks[index], captureTicks);

            void Fail(string kind, int position)
            {
                var ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
                object? Describe(object? value)
                {
                    if (value is null) return null;
                    if (value is int or bool) return value;
                    if (!ids.TryGetValue(value, out int id)) { id = ids.Count; ids.Add(value, id); }
                    return new { reference = id, type = value.GetType().FullName };
                }
                // One failure scene only, retained by the existing per-seed stderr receipt.
                Console.Error.WriteLine("HOOK_LISTENER_CACHE_FIRST_DIFFERENCE " + JsonSerializer.Serialize(new
                {
                    seed = root.Rng.Seed, phase = phase.ToString(), kind, position, version = snapshot.Version,
                    round = state.RoundNumber, side = state.CurrentSide.ToString(),
                    old = old.Select(Describe).ToArray(), cached = snapshot.Full.Select(Describe).ToArray(),
                    sourceWitness = snapshot._witness.Select(Describe).ToArray(),
                }));
                throw new InvalidOperationException($"Hook listener cache differs before execution: {phase}/{kind}, position={position}, version={snapshot.Version}.");
            }
        }

        private static bool Overrides(AbstractModel model, HookListenerPhase phase) =>
            OverrideOracle.GetOrAdd((model.GetType(), phase), static key =>
            {
                bool cost = key.Item2 <= HookListenerPhase.Star;
                string method = key.Item2 switch
                {
                    HookListenerPhase.Energy => nameof(AbstractModel.TryModifyEnergyCostInCombat),
                    HookListenerPhase.EnergyLate => nameof(AbstractModel.TryModifyEnergyCostInCombatLate),
                    HookListenerPhase.Star => nameof(AbstractModel.TryModifyStarCostInCombat),
                    HookListenerPhase.AfterPiles => nameof(AbstractModel.AfterCardChangedPiles),
                    _ => nameof(AbstractModel.AfterCardChangedPilesLate),
                };
                Type[] parameters = cost
                    ? [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()]
                    : [typeof(CardModel), typeof(PileType), typeof(AbstractModel)];
                MethodInfo slot = typeof(AbstractModel).GetMethod(method, BindingFlags.Instance | BindingFlags.Public,
                    binder: null, parameters, modifiers: null)!;
                var instance = (AbstractModel)RuntimeHelpers.GetUninitializedObject(key.Item1);
                Delegate target = Delegate.CreateDelegate(cost ? typeof(CostHook) : typeof(PileHook), instance, slot);
                return target.Method.DeclaringType != typeof(AbstractModel);
            });
    }
#endif
}
