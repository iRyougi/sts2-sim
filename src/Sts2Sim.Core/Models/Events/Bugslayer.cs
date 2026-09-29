using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

public sealed class Bugslayer : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("EXTERMINATION", () => AddAsync<Exterminate>()),
        new EventOption("SQUASH", () => AddAsync<Squash>()),
    ];

    private async Task AddAsync<TCard>() where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
        Finish();
    }
}
