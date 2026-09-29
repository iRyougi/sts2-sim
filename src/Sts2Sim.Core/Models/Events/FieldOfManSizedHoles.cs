using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class FieldOfManSizedHoles : EventModel
{
    public override bool IsAllowed(IRunState runState)
    {
        PerfectFit perfectFit = ModelDb.GetById<PerfectFit>(ModelDb.GetId<PerfectFit>());
        return runState.Players.All(player => player.Deck.Cards.Any(perfectFit.CanEnchant));
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("RESIST", ResistAsync),
        new EventOption("ENTER_YOUR_HOLE", EnterAsync),
    ];

    private async Task ResistAsync()
    {
        foreach (CardModel card in (await CardSelectCmd.FromDeckForRemoval(Owner, 2, this)).ToList())
            await CardPileCmd.RemoveFromDeck(Owner, card);
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Normality>()], Owner);
        Finish();
    }

    private async Task EnterAsync()
    {
        PerfectFit perfectFit = ModelDb.GetById<PerfectFit>(ModelDb.GetId<PerfectFit>());
        CardModel? selected = Owner.Deck.Cards.FirstOrDefault(perfectFit.CanEnchant);
        if (selected is not null) await CardCmd.Enchant<PerfectFit>(selected, 1m);
        Finish();
    }
}
