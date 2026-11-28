using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Afflictions;

namespace Sts2Sim.Core.Hooks;

/// <summary>
/// Immutable type capabilities for the three neutral cost slots, two completed-Task pile slots,
/// and 104 single-overload Hook dispatch slots. No instance, eligibility, notification or Hook result is cached.
/// </summary>
internal static class HookOverrideIndex
{
    internal readonly record struct HookSkipMask(ulong Lo, ulong Hi);

    [Flags]
    internal enum CostSlot : byte { Energy = 1, EnergyLate = 2, Star = 4, All = 7 }
    [Flags]
    internal enum PileSlot : byte { Normal = 1, Late = 2, All = 3 }

    private static readonly ConcurrentDictionary<Type, byte> CostCoverage = new();
    private static readonly ConcurrentDictionary<Type, byte> PileCoverage = new();
    private static readonly MethodInfo[] BaseSlots =
    [
        ResolveBaseSlot(nameof(AbstractModel.TryModifyEnergyCostInCombat),
            [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()]),
        ResolveBaseSlot(nameof(AbstractModel.TryModifyEnergyCostInCombatLate),
            [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()]),
        ResolveBaseSlot(nameof(AbstractModel.TryModifyStarCostInCombat),
            [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()]),
    ];
    private static readonly MethodInfo[] BasePileSlots =
    [
        ResolveBaseSlot(nameof(AbstractModel.AfterCardChangedPiles),
            [typeof(CardModel), typeof(PileType), typeof(AbstractModel)]),
        ResolveBaseSlot(nameof(AbstractModel.AfterCardChangedPilesLate),
            [typeof(CardModel), typeof(PileType), typeof(AbstractModel)]),
    ];

    /// <summary>
    /// Hooks whose dispatch loop may pass over a listener that inherits <see cref="AbstractModel"/>'s own implementation
    /// (a no-op: a completed task, the value passed in, 0 / 1 / <see cref="decimal.MaxValue"/>, true or false). Plan 09-O.
    /// </summary>
    internal enum HookSlot : byte
    {
        ModifyDamageAdditive, ModifyDamageMultiplicative, ModifyDamageMultiplicativeLate, ModifyDamageCap,
        ModifyHpLostBeforeOsty, ModifyHpLostBeforeOstyLate, ModifyHpLostAfterOsty, ModifyHpLostAfterOstyLate,
        AfterModifyingHpLostBeforeOsty, AfterModifyingHpLostAfterOsty,
        ShouldAllowHitting, BeforeDamageReceived, ModifyPreBlockDamageTarget, ModifyUnblockedDamageTarget,
        AfterDamageGiven, AfterDamageReceived, AfterCurrentHpChanged,
        TryModifyPowerAmountReceived, AfterModifyingPowerAmountReceived, BeforePowerAmountChanged, AfterPowerAmountChanged,
        ModifyPowerAmountGivenAdditive, ModifyPowerAmountGivenMultiplicative,
        ModifyBlockAdditive, ModifyBlockMultiplicative,
        BeforeAttack, AfterAttack, ModifyAttackHitCount,
        AfterSideTurnStart, AfterSideTurnStartLate,
        BeforeSideTurnEndVeryEarly, BeforeSideTurnEndEarly, BeforeSideTurnEnd, AfterSideTurnEnd, AfterSideTurnEndLate,
        BeforeFlush, BeforeFlushLate, ShouldTakeExtraTurnEarly, ShouldTakeExtraTurn,
        ShouldClearBlock, AfterBlockCleared,
        ShouldPlay, BeforeCardPlayed, AfterCardPlayed, AfterCardPlayedLate,
        ModifyCardPlayCount, AfterModifyingCardPlayCount,
        ModifyCardPlayResultLocation, AfterModifyingCardPlayResultLocation,
        AfterEnergySpent, AfterStarsSpent, AfterStarsGained, ShouldGainStars,
        BeforeBlockGained, AfterBlockGained, AfterBlockBroken, AfterModifyingBlockAmount,
        BeforeHandDraw, ModifyHandDraw, AfterModifyingHandDraw, ShouldDraw,
        AfterCardDrawnEarly, AfterCardDrawn,
        AfterCardDiscarded, AfterCardExhausted, AfterShuffle, AfterHandEmptied,
        AfterCardGeneratedForCombat, AfterCardEnteredCombat,
        ShouldEtherealTrigger, ShouldFlush, AfterFlush,
        ModifyMaxEnergy, ModifyXValue, ShouldPlayerResetEnergy, AfterEnergyReset, AfterEnergyResetLate,
        BeforeSideTurnStart, AfterPlayerTurnStartEarly, AfterPlayerTurnStart, AfterPlayerTurnStartLate,
        AfterAutoPrePlayPhaseEnteredEarly, AfterAutoPrePlayPhaseEntered, AfterAutoPrePlayPhaseEnteredLate, AfterAutoPostPlayPhaseEntered,
        ShouldDie, ShouldDieLate, BeforeDeath,
        ShouldCreatureBeRemovedFromCombatAfterDeath, ShouldPowerBeRemovedOnDeath, ShouldStopCombatFromEnding,
        TryModifyKeywordsInCombat,
        AfterOrbChanneled, AfterOrbEvoked, ModifyOrbPassiveTriggerCounts, ModifyOrbValue,
        ModifySummonAmount, AfterSummon, AfterOstyRevived, AfterForge,
        AfterArrowShot, AfterArrowImpaled, AfterArrowDislodged, AfterDiedToDoom,
        Count,
    }

    private static readonly MethodInfo[] HookSlots = ResolveHookSlots();

    private static MethodInfo[] ResolveHookSlots()
    {
        if ((int)HookSlot.Count > 128)
        {
            throw new InvalidOperationException($"HookSlot.Count ({(int)HookSlot.Count}) exceeds 128-bit mask capacity.");
        }
        var slots = new MethodInfo[(int)HookSlot.Count];
        for (int i = 0; i < slots.Length; i++)
        {
            string name = ((HookSlot)i).ToString();
            MethodInfo[] found = typeof(AbstractModel).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(method => method.Name == name && method.IsVirtual).ToArray();
            // Exactly one virtual of that name, so a slot can never stand for an overload it was not reviewed for.
            slots[i] = found.Length == 1 ? found[0]
                : throw new MissingMethodException(typeof(AbstractModel).FullName, $"{name} ({found.Length} virtual overloads)");
        }
        return slots;
    }

    /// <summary>
    /// True when <paramref name="model"/> is a known hook-listener kind (so <c>Hook.IsEligibleHookListener</c> is a pure read
    /// for it) and its runtime type inherits <see cref="AbstractModel"/>'s implementation of <paramref name="slot"/>, which
    /// does nothing. Null or unknown listener kinds return false so dispatch reaches the fail-closed eligibility guard.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Skips(AbstractModel? model, HookSlot slot)
    {
        if (model is null)
        {
            return false;
        }
        int s = (int)slot;
        return s < 64
            ? (model.HookSkipBits & (1UL << s)) != 0
            : (model.HookSkipBitsHi & (1UL << (s - 64))) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool HasActive(ulong activeLo, ulong activeHi, HookSlot slot)
    {
        int s = (int)slot;
        return s < 64
            ? (activeLo & (1UL << s)) != 0
            : (activeHi & (1UL << (s - 64))) != 0;
    }

    /// <summary>The hook-listener kind of a model type, in <c>Hook.IsEligibleHookListener</c>'s order (the kinds are
    /// disjoint direct subclasses of <see cref="AbstractModel"/>).</summary>
    internal enum ListenerKind : byte { Unknown, Power, Monster, Orb, Passive }

    internal static ListenerKind KindOf(Type type) =>
        typeof(PowerModel).IsAssignableFrom(type) ? ListenerKind.Power
        : typeof(MonsterModel).IsAssignableFrom(type) ? ListenerKind.Monster
        : typeof(OrbModel).IsAssignableFrom(type) ? ListenerKind.Orb
        : typeof(CardModel).IsAssignableFrom(type) || typeof(RelicModel).IsAssignableFrom(type)
            || typeof(PotionModel).IsAssignableFrom(type) || typeof(AfflictionModel).IsAssignableFrom(type)
            || typeof(EnchantmentModel).IsAssignableFrom(type) || typeof(RunModel).IsAssignableFrom(type) ? ListenerKind.Passive
        : ListenerKind.Unknown;

    // Read-mostly table keyed by runtime type (reference identity), replaced whole under a lock when a new type appears.
    private sealed class SkipTable(Type?[] keys, HookSkipMask[] bits, int count)
    {
        public readonly Type?[] Keys = keys;
        public readonly HookSkipMask[] Bits = bits;
        public readonly int Count = count;
    }

    private static volatile SkipTable _skips = new(new Type?[256], new HookSkipMask[256], 0);
    private static readonly object SkipGate = new();

    /// <summary>The per-type bits behind <see cref="Skips"/>, read once per model type by <see cref="AbstractModel"/>.</summary>
    internal static HookSkipMask SkipBitsOf(Type type)
    {
        SkipTable table = _skips;
        int mask = table.Keys.Length - 1;
        for (int i = RuntimeHelpers.GetHashCode(type) & mask; ; i = (i + 1) & mask)
        {
            Type? key = table.Keys[i];
            if (ReferenceEquals(key, type)) return table.Bits[i];
            if (key is null) return AddSkipBits(type);
        }
    }

    private static HookSkipMask AddSkipBits(Type type)
    {
        lock (SkipGate)
        {
            SkipTable table = _skips;
            int length = table.Keys.Length;
            if ((table.Count + 1) * 2 > length) length *= 2;
            var keys = new Type?[length];
            var bits = new HookSkipMask[length];
            HookSkipMask added = default;
            bool present = false;
            void Insert(Type key, HookSkipMask value)
            {
                for (int i = RuntimeHelpers.GetHashCode(key) & (length - 1); ; i = (i + 1) & (length - 1))
                {
                    if (keys[i] is null) { keys[i] = key; bits[i] = value; return; }
                    if (ReferenceEquals(keys[i], key)) return;
                }
            }
            for (int i = 0; i < table.Keys.Length; i++)
            {
                if (table.Keys[i] is not { } key) continue;
                Insert(key, table.Bits[i]);
                if (ReferenceEquals(key, type)) { present = true; added = table.Bits[i]; }
            }
            if (present) return added;
            added = ResolveSkipBits(type);
            Insert(type, added);
            _skips = new SkipTable(keys, bits, table.Count + 1);
            return added;
        }
    }

    internal const ulong UnknownOrNullSentinelHiBit = 1UL << 63;

    private static HookSkipMask ResolveSkipBits(Type type)
    {
        if (KindOf(type) == ListenerKind.Unknown)
            return default; // unknown kinds keep all skip bits 0 (including bit 127) so dispatch reaches fail-closed guard
        // ResolveCoverage sets a bit for every slot the type may override (or cannot be resolved); the rest are skipped.
        ulong coveredLo = 0;
        for (int start = 0; start < 64; start += 8)
        {
            MethodInfo[] group = HookSlots[start..(start + 8)];
            coveredLo |= (ulong)ResolveCoverage(type, group, 0xFF) << start;
        }

        ulong coveredHi = 0;
        int hiCount = HookSlots.Length - 64;
        for (int start = 0; start < hiCount; start += 8)
        {
            MethodInfo[] group = HookSlots[(64 + start)..Math.Min(64 + start + 8, HookSlots.Length)];
            coveredHi |= (ulong)ResolveCoverage(type, group, (byte)((1 << group.Length) - 1)) << start;
        }

        // Unused high bits (including bit 63 = UnknownOrNullSentinelHiBit) stay 1 in skipHi (0 in ~skipHi) for known types.
        return new HookSkipMask(~coveredLo, ~coveredHi);
    }

    internal static bool MayOverride(AbstractModel model, CostSlot slot) =>
        (CostCoverage.GetOrAdd(model.GetType(), static type => ResolveCoverage(type, BaseSlots, (byte)CostSlot.All))
            & (byte)slot) != 0;

    internal static bool MayOverride(AbstractModel model, PileSlot slot) =>
        (PileCoverage.GetOrAdd(model.GetType(), static type => ResolveCoverage(type, BasePileSlots, (byte)PileSlot.All))
            & (byte)slot) != 0;

    private static MethodInfo ResolveBaseSlot(string name, Type[] parameters) =>
        typeof(AbstractModel).GetMethod(name, BindingFlags.Instance | BindingFlags.Public,
            binder: null, parameters, modifiers: null)
        ?? throw new MissingMethodException(typeof(AbstractModel).FullName, name);

    private static byte ResolveCoverage(Type runtimeType, MethodInfo[] baseSlots, byte all)
    {
        try
        {
            byte coverage = 0;
            for (int index = 0; index < baseSlots.Length; index++)
            {
                MethodInfo baseSlot = baseSlots[index];
                bool resolved = false;
                for (Type? current = runtimeType; current is not null; current = current.BaseType)
                {
                    // DeclaredOnly keeps hidden new slots from obscuring inherited overrides.
                    foreach (MethodInfo method in current.GetMethods(BindingFlags.Instance |
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (!method.IsVirtual || method.GetBaseDefinition() != baseSlot) continue;
                        if (method != baseSlot) coverage |= (byte)(1 << index);
                        resolved = true;
                        break;
                    }
                    if (resolved) break;
                }
                if (!resolved) coverage |= (byte)(1 << index);
            }
            return coverage;
        }
        catch (Exception error) when (error is not OutOfMemoryException and not AccessViolationException)
        {
            // fail-open: Metadata-only failure: retain every original call. Fatal failures propagate.
            return all;
        }
    }
}
