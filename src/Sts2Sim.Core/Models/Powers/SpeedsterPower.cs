using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class SpeedsterPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (!fromHandDraw && card.Owner.Creature == Owner && Owner.CombatState!.CurrentSide == Owner.Side)
        {
            await CreatureCmd.Damage(
                Owner.CombatState, Owner.CombatState.HittableEnemies, Amount,
                ValueProp.Unpowered, Owner, null, null);
        }
    }
}
