using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

public sealed class Bugslayer : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("EXTERMINATION", [VisibleValue.Int("ObtainCount", 1), VisibleValue.Id("Card1", "EXTERMINATE")]),
        new("SQUASH", [VisibleValue.Int("ObtainCount", 1), VisibleValue.Id("Card2", "SQUASH")])
    ]);

    protected override VisibleOption DescribeVisibleOption(EventOption option, int displayIndex, VisibleOption declared) =>
        option.Key switch
        {
            "EXTERMINATION" => declared with { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Exterminate>())] },
            "SQUASH" => declared with { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Squash>())] },
            _ => base.DescribeVisibleOption(option, displayIndex, declared),
        };

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
