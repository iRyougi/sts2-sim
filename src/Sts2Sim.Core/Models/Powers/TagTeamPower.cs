using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class TagTeamPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (card.Type != CardType.Attack || card.Owner.Creature == Applier)
        {
            return playCount;
        }

        if (card.TargetType == TargetType.AnyEnemy && target != Owner)
        {
            return playCount;
        }

        if (card.TargetType is not (TargetType.AnyEnemy or TargetType.AllEnemies))
        {
            return playCount;
        }

        return playCount + Amount;
    }

    public override Task AfterModifyingCardPlayCount(CardModel card) =>
        PowerCmd.Remove(this);
}
