using Sts2Sim.Core.Events;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class GraveOfTheForgotten : EventModel
{
    public override bool IsAllowed(IRunState runState)
    {
        SoulsPower souls = ModelDb.GetById<SoulsPower>(ModelDb.GetId<SoulsPower>());
        return runState.Players.All(player => player.Deck.Cards.Any(souls.CanEnchant));
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        SoulsPower souls = ModelDb.GetById<SoulsPower>(ModelDb.GetId<SoulsPower>());
        bool canConfront = Owner.Deck.Cards.Any(souls.CanEnchant);
        return
        [
            new EventOption("CONFRONT", canConfront ? ConfrontAsync : null),
            new EventOption("ACCEPT", AcceptAsync),
        ];
    }

    private async Task ConfrontAsync()
    {
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Decay>()], Owner);
        SoulsPower souls = ModelDb.GetById<SoulsPower>(ModelDb.GetId<SoulsPower>());
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            RunState,
            Owner,
            Owner.Deck.Cards.Where(souls.CanEnchant),
            1,
            1,
            this)).FirstOrDefault();
        if (selected is not null)
        {
            await CardCmd.Enchant<SoulsPower>(selected, 1m);
        }

        Finish();
    }

    private async Task AcceptAsync()
    {
        await RelicCmd.Obtain(ModelDb.Relic<ForgottenSoul>(), Owner);
        Finish();
    }
}
