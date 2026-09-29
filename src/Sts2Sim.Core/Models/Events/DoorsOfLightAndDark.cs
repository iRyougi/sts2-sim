using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Events;

public sealed class DoorsOfLightAndDark : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("LIGHT", LightAsync),
        new EventOption("DARK", DarkAsync),
    ];

    private Task LightAsync()
    {
        List<CardModel> candidates = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        candidates.StableShuffle(Rng);
        foreach (CardModel card in candidates.Take(2))
            CardCmd.Upgrade(card);

        Finish();
        return Task.CompletedTask;
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
