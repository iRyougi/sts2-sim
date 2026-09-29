namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

public sealed class ShriekPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override bool AllowNegative => true;

    public override async Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (ReferenceEquals(target, Owner) && result.UnblockedDamage > 0 && target.CurrentHp <= Amount &&
            Owner.Monster is TerrorEel eel)
        {
            await CreatureCmd.Stun(Owner, eel.TerrorState.StateId);
            await PowerCmd.Remove(this);
        }
    }
}
