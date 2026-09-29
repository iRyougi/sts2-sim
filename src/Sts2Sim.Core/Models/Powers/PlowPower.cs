using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Breaks when a damaging hit leaves the owner at or below the Power amount.</summary>
public sealed class PlowPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 || target.CurrentHp > Amount)
        {
            return;
        }

        foreach (TemporaryStrengthPower temporaryStrength in
                 Owner.Powers.OfType<TemporaryStrengthPower>().ToArray())
        {
            await PowerCmd.Remove(temporaryStrength);
        }

        if (Owner.GetPower<StrengthPower>() is { } strength)
        {
            await PowerCmd.Remove(strength);
        }

        if (Owner.Monster is CeremonialBeast beast)
        {
            beast.SetStunned();
        }
        else
        {
            await CreatureCmd.Stun(Owner);
        }

        await PowerCmd.Remove(this);
    }
}
