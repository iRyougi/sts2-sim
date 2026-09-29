using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class SpiritGrafter : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("LET_IT_IN", LetInAsync),
        new EventOption("REJECTION", RejectAsync),
    ];

    private async Task LetInAsync()
    {
        await CreatureCmd.Heal(Owner.Creature, 25m);
        var card = (Metamorphosis)ModelDb.Card<Metamorphosis>().MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
        Finish();
    }

    private async Task RejectAsync()
    {
        CardModel? card = (await CardSelectCmd.FromDeckForUpgrade(Owner, 1, this)).FirstOrDefault();
        if (card is not null) CardCmd.Upgrade(card);
        await CreatureCmd.Damage(RunState, Owner.Creature, 10m, ValueProp.Unblockable | ValueProp.Unpowered);
        Finish();
    }
}
