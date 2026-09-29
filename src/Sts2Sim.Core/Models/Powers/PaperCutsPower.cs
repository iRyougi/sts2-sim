namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class PaperCutsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource) =>
        dealer == Owner && target.IsPlayer && props.IsPoweredAttack() && result.UnblockedDamage > 0
            ? CreatureCmd.LoseMaxHp(Owner.CombatState!.RunState, target, Amount, isFromCard: false)
            : Task.CompletedTask;
}
