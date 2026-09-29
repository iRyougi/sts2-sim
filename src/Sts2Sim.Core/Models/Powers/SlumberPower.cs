namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

public sealed class SlumberPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target == Owner && result.UnblockedDamage > 0) await Tick(stun: true);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner)) await Tick(stun: false);
    }

    private async Task Tick(bool stun)
    {
        if (!Owner.Powers.Contains(this)) return;
        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        if (!Owner.Powers.Contains(this) && Owner.Monster is SlumberingBeetle beetle)
        {
            if (stun)
            {
                beetle.SetWakeUpStunned();
            }
            else
            {
                await beetle.WakeUp();
                beetle.SetRollOut();
            }
        }
    }
}
