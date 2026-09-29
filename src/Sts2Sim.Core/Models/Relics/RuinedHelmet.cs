using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Double the first positive Strength amount the owner receives each combat.</summary>
public sealed class RuinedHelmet : RelicModel
{
    private bool _usedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool TryModifyPowerAmountReceived(
        PowerModel power, Creature target, decimal amount,
        Creature? giver, out decimal modifiedAmount)
    {
        _ = giver;
        modifiedAmount = amount;
        if (power is not StrengthPower || !ReferenceEquals(target, Owner.Creature) ||
            amount <= 0m || _usedThisCombat)
            return false;
        modifiedAmount *= 2m;
        return true;
    }

    public override Task AfterModifyingPowerAmountReceived(PowerModel power)
    {
        _ = power;
        _usedThisCombat = true;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _usedThisCombat = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_usedThisCombat);
    }
}
