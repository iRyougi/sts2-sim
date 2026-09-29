using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class RoomFullOfCheese : EventModel
{
    public override bool IsAllowed(IRunState runState) => runState is not RunState { CurrentActIndex: >= 2 };
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("GORGE", Gorge), new("SEARCH", Search)];

    private async Task Gorge()
    {
        var options = CardCreationOptions.ForNonCombatWithUniformOdds([Owner.Character.CardPool],
            c => c.Rarity == CardRarity.Common).WithFlags(CardCreationFlags.NoRarityModification);
        var cards = CardFactory.CreateForReward(Owner, 8, options);
        foreach (var card in await CardSelectCmd.SelectCardsAsync(RunState, Owner, cards, 2, 2, this))
            await CardPileCmd.AddToDeck(card);
        Finish();
    }
    private async Task Search()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, 14m, ValueProp.Unblockable | ValueProp.Unpowered);
        await RelicCmd.Obtain(ModelDb.Relic<ChosenCheese>(), Owner);
        Finish();
    }
}
