using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class BrainLeech : EventModel
{
    public override bool IsAllowed(IRunState runState) => runState is not RunState { CurrentActIndex: >= 2 };
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("SHARE_KNOWLEDGE", ShareKnowledge), new("RIP", Rip)];

    private async Task ShareKnowledge()
    {
        var cards = CardFactory.CreateForReward(Owner, 5,
            CardCreationOptions.ForNonCombatWithDefaultOdds([Owner.Character.CardPool]));
        foreach (var card in await CardSelectCmd.SelectCardsAsync(RunState, Owner, cards, 1, 1, this))
            await CardPileCmd.AddToDeck(card);
        Finish();
    }

    private async Task Rip()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, 5m, ValueProp.Unblockable | ValueProp.Unpowered);
        if (Owner.Creature.IsDead) { Finish(); return; }
        var reward = new CardReward(Owner,
            CardCreationOptions.ForNonCombatWithDefaultOdds([ColorlessCardPool.Instance])
                .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications));
        reward.Populate(RunState);
        OfferRewards(RewardsSet.CreateCustom(Owner, card: reward));
        Finish();
    }
}
