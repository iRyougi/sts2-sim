namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

public sealed class FlutterPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) =>
        target == Owner && props.IsPoweredAttack() ? 0.5m : 1m;

    public override async Task AfterDamageReceived(Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 || !props.IsPoweredAttack()) return;
        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        if (!Owner.Powers.Contains(this))
        {
            if (Owner.Monster is ThievingHopper hopper)
            {
                hopper.StopHovering();
            }
            MonsterModel monster = Owner.Monster
                ?? throw new InvalidOperationException("Flutter can only belong to a monster.");
            string nextState = monster.MoveStateMachine!.StateLog.Last().GetNextState(
                Owner,
                monster.RunRng.MonsterAi)
                ?? throw new InvalidOperationException("Flutter's triggering state must have a follow-up.");
            await CreatureCmd.Stun(Owner, nextState);
        }
    }
}
