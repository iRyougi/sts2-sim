using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class ZenWeaver : EventModel
{
    public override bool IsAllowed(IRunState runState) => runState.Players.All(player => player.Gold >= 125);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("BREATHING_TECHNIQUES", Owner.Gold >= 50 ? BreathingAsync : null),
        new EventOption("EMOTIONAL_AWARENESS", Owner.Gold >= 125 ? () => RemoveAsync(125, 1) : null),
        new EventOption("ARACHNID_ACUPUNCTURE", Owner.Gold >= 250 ? () => RemoveAsync(250, 2) : null),
    ];

    private async Task BreathingAsync()
    {
        await PlayerCmd.LoseGold(50, Owner, GoldLossType.Spent);
        for (int i = 0; i < 2; i++)
        {
            var card = (Enlightenment)ModelDb.Card<Enlightenment>().MutableClone();
            card.AssignOwner(Owner);
            await CardPileCmd.AddToDeck(card);
        }
        Finish();
    }

    private async Task RemoveAsync(int cost, int count)
    {
        foreach (CardModel card in (await CardSelectCmd.FromDeckForRemoval(Owner, count, this)).ToList())
            await CardPileCmd.RemoveFromDeck(Owner, card);
        await PlayerCmd.LoseGold(cost, Owner, GoldLossType.Spent);
        Finish();
    }
}
