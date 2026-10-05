using System.Collections.Concurrent;
using System.Reflection;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Hooks;

/// <summary>
/// Immutable type capabilities for the three neutral cost slots and two completed-Task
/// pile slots. No instance, eligibility, notification or Hook result is cached.
/// </summary>
internal static class HookOverrideIndex
{
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
            // Metadata-only failure: retain every original call. Fatal failures propagate.
            return all;
        }
    }
}
