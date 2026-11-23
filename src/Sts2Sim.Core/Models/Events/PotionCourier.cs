using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class PotionCourier : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("GRAB_POTIONS", [VisibleValue.Id("Potion", "FOUL_POTION"), VisibleValue.Int("Count", 3)])
        { Contents = [VisibleContentProjection.ForPotion(ModelDb.Potion<FoulPotion>())] },
        new("RANSACK", [VisibleValue.Scope("RewardKind", "Potion"), VisibleValue.Int("Count", 1),
            VisibleValue.Scope("Rarity", "Uncommon"), VisibleValue.Condition("Targets", "Random")]),
    ]);

    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: > 0 };
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("GRAB_POTIONS", GrabPotions), new("RANSACK", Ransack)];

    private Task GrabPotions()
    {
        var rewards = Enumerable.Range(0, 3).Select(_ =>
            (Reward)new PotionReward((PotionModel)ModelDb.Potion<FoulPotion>().MutableClone(), Owner)).ToArray();
        OfferRewards(RewardsSet.CreateCustom(Owner, extraRewards: rewards));
        Finish();
        return Task.CompletedTask;
    }

    private Task Ransack()
    {
        var potion = Owner.PlayerRng.Rewards.NextItem(PotionFactory.GetOutOfCombatPool(Owner)
            .Where(p => p.Rarity == PotionRarity.Uncommon));
        if (potion is not null)
            OfferRewards(RewardsSet.CreateCustom(Owner,
                potion: new PotionReward((PotionModel)potion.MutableClone(), Owner)));
        Finish();
        return Task.CompletedTask;
    }
}
