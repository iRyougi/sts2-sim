using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;

namespace Sts2Sim.Core.Models.Events;

public sealed class DoorsOfLightAndDark : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("LIGHT", [VisibleValue.Int("UpgradeSelectCount", 1)]),
        new("DARK", [VisibleValue.Int("RemoveCount", 1)])
    ]);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("LIGHT", LightAsync),
        new EventOption("DARK", DarkAsync),
    ];

    private async Task LightAsync()
    {
        CardModel? card = (await CardSelectCmd.FromDeckForUpgrade(Owner, 1, this)).FirstOrDefault();
        if (card is not null)
            CardCmd.Upgrade(card);

        Finish();
    }

    private async Task DarkAsync()
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            RunState,
            Owner,
            Owner.Deck.Cards.Where(card => card.IsRemovable),
            1,
            1,
            this);
        foreach (CardModel card in selected)
            await CardPileCmd.RemoveFromDeck(Owner, card);

        Finish();
    }
}
