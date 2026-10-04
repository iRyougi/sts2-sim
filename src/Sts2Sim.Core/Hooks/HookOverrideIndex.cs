using System.Collections.Concurrent;
using System.Reflection;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Hooks;

/// <summary>
/// Immutable type capabilities for the three cost slots whose base bodies only return
/// false and copy the input decimal to the out parameter. No instance or result is cached.
/// </summary>
internal static class HookOverrideIndex
{
    [Flags]
    internal enum CostSlot : byte { Energy = 1, EnergyLate = 2, Star = 4, All = 7 }

    private static readonly ConcurrentDictionary<Type, CostSlot> Coverage = new();
    private static readonly MethodInfo[] BaseSlots =
    [
        ResolveBaseSlot(nameof(AbstractModel.TryModifyEnergyCostInCombat)),
        ResolveBaseSlot(nameof(AbstractModel.TryModifyEnergyCostInCombatLate)),
        ResolveBaseSlot(nameof(AbstractModel.TryModifyStarCostInCombat)),
    ];

    internal static bool MayOverride(AbstractModel model, CostSlot slot) =>
        (Coverage.GetOrAdd(model.GetType(), ResolveCoverage) & slot) != 0;

    private static MethodInfo ResolveBaseSlot(string name) =>
        typeof(AbstractModel).GetMethod(name, BindingFlags.Instance | BindingFlags.Public,
            binder: null, [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()],
            modifiers: null) ?? throw new MissingMethodException(typeof(AbstractModel).FullName, name);

    private static CostSlot ResolveCoverage(Type runtimeType)
    {
        try
        {
            CostSlot coverage = 0;
            for (int index = 0; index < BaseSlots.Length; index++)
            {
                MethodInfo baseSlot = BaseSlots[index];
                bool resolved = false;
                for (Type? current = runtimeType; current is not null; current = current.BaseType)
                {
                    // DeclaredOnly is intentional: GetMethod may select a hidden new virtual
                    // slot and obscure an inherited override of AbstractModel's actual slot.
                    foreach (MethodInfo method in current.GetMethods(BindingFlags.Instance |
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (!method.IsVirtual || method.GetBaseDefinition() != baseSlot)
                            continue;
                        if (method != baseSlot)
                            coverage |= (CostSlot)(1 << index);
                        resolved = true;
                        break;
                    }
                    if (resolved) break;
                }
                if (!resolved) coverage |= (CostSlot)(1 << index);
            }
            return coverage;
        }
        catch (Exception error) when (error is not OutOfMemoryException and not AccessViolationException)
        {
            // The try body queries metadata only. Any recoverable lookup failure retains
            // the old calls, including unusual Type implementations and missing dependencies.
            // Fatal runtime failures must propagate; an unknown slot is never neutral.
            return CostSlot.All;
        }
    }
}
