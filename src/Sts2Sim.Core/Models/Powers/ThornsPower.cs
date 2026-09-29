using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ThornsPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeDamageReceived(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner ||
            dealer is null ||
            (!props.IsPoweredAttack() && cardSource is not Omnislice))
        {
            return;
        }

        await CreatureCmd.Damage(
            Owner.CombatState!,
            new[] { dealer },
            Amount,
            ValueProp.Unpowered | ValueProp.SkipHurtAnim,
            Owner,
            null,
            null);
    }
}
