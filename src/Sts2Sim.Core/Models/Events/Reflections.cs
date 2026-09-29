using Sts2Sim.Core.Events;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

public sealed class Reflections : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("TOUCH_A_MIRROR", TouchAMirrorAsync),
        new EventOption("SHATTER", ShatterAsync),
    ];

    private Task TouchAMirrorAsync()
    {
        List<CardModel> upgraded = Owner.Deck.Cards.Where(card => card.IsUpgraded).ToList();
        for (int i = 0; i < 2 && upgraded.Count > 0; i++)
        {
            CardModel card = Rng.NextItem(upgraded)!;
            upgraded.Remove(card);
            card.Downgrade();
        }

        List<CardModel> upgradable = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        for (int i = 0; i < 4 && upgradable.Count > 0; i++)
        {
            CardModel card = Rng.NextItem(upgradable)!;
            upgradable.Remove(card);
            CardCmd.Upgrade(card);
        }

        Finish();
        return Task.CompletedTask;
    }

    private async Task ShatterAsync()
    {
        CardModel[] originals = Owner.Deck.Cards.ToArray();
        foreach (CardModel original in originals)
        {
            var copy = (CardModel)original.MutableClone();
            copy.AssignOwner(Owner);
            await CardPileCmd.AddToDeck(copy, original);
        }

        await CardPileCmd.AddCursesToDeck([ModelDb.Card<BadLuck>()], Owner);
        Finish();
    }
}
