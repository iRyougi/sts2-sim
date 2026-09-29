namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

public sealed class ImbalancedPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (ReferenceEquals(dealer, Owner) && result.BlockedDamage > 0 && result.UnblockedDamage == 0)
        {
            if (Owner.Monster is BowlbugRock rock)
            {
                rock.IsOffBalance = true;
            }
            else
            {
                return CreatureCmd.Stun(Owner);
            }
        }

        return Task.CompletedTask;
    }
}
