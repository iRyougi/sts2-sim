using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class TheLegendsWereTrue : EventModel
{
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: 0 } &&
        runState.Players.All(p => p.Deck.Cards.Count > 0 && p.Creature.CurrentHp >= 10);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("NAB_THE_MAP", NabTheMap), new("SLOWLY_FIND_AN_EXIT", SlowlyFindAnExit)];

    private async Task NabTheMap()
    {
        var card = (SpoilsMap)ModelDb.Card<SpoilsMap>().MutableClone();
        card.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(card);
        Finish();
    }

    private async Task SlowlyFindAnExit()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, 8, ValueProp.Unblockable | ValueProp.Unpowered);
        var potion = Owner.PlayerRng.Rewards.NextItem(PotionFactory.GetOutOfCombatPool(Owner));
        if (potion is not null)
            OfferRewards(RewardsSet.CreateCustom(Owner,
                potion: new PotionReward((PotionModel)potion.MutableClone(), Owner)));
        Finish();
    }
}
