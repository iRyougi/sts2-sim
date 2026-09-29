using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ReboundPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override CardLocation ModifyCardPlayResultLocation(CardModel card, bool isAutoPlay,
        ResourceInfo resources, CardLocation location) =>
        card.Owner.Creature == Owner && location.PileType == PileType.Discard
            ? location with { PileType = PileType.Draw, Position = CardPilePosition.Top }
            : location;

    public override async Task AfterModifyingCardPlayResultLocation(CardModel card, CardLocation location)
    {
        if (card.Owner.Creature == Owner)
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
