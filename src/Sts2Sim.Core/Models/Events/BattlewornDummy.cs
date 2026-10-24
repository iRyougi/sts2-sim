using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Models.Events;

public sealed class BattlewornDummy : EventModel
{
    private BattlewornRewardTier? _selectedTier;

    protected override VisibleDecisionMetadata BuildVisibleState() => new([VisibleValue.Int("TurnLimit", 3)], [
        new("SETTING_1", [VisibleValue.Int("Setting1Hp", 75), VisibleValue.Int("PotionCount", 1)]),
        new("SETTING_2", [VisibleValue.Int("Setting2Hp", 150), VisibleValue.Int("RandomUpgradeCount", 2)]),
        new("SETTING_3", [VisibleValue.Int("Setting3Hp", 300), VisibleValue.Int("RelicCount", 1)])
    ]);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("SETTING_1", () => ChooseSettingAsync<BattleFriendV1>(BattlewornRewardTier.Potion)),
        new EventOption("SETTING_2", () => ChooseSettingAsync<BattleFriendV2>(BattlewornRewardTier.Upgrades)),
        new EventOption("SETTING_3", () => ChooseSettingAsync<BattleFriendV3>(BattlewornRewardTier.Relic)),
    ];

    protected override void AfterForcedCombat(ForcedCombatOutcome outcome)
    {
        if (outcome.Victory && !outcome.TimedOut)
        {
            GrantTierReward(_selectedTier ?? throw new InvalidOperationException("Battleworn Dummy has no selected reward tier."));
        }

        Finish();
    }

    private Task ChooseSettingAsync<TMonster>(BattlewornRewardTier rewardTier)
        where TMonster : MonsterModel
    {
        _selectedTier = rewardTier;
        RequestForcedCombatBatch(() => [(MonsterModel)ModelDb.Monster<TMonster>().MutableClone()]);
        SuspendForForcedCombat();
        return Task.CompletedTask;
    }

    private void GrantTierReward(BattlewornRewardTier rewardTier)
    {
        switch (rewardTier)
        {
            case BattlewornRewardTier.Potion:
                PotionModel? potion = Owner.PlayerRng.Rewards.NextItem(PotionFactory.GetOutOfCombatPool(Owner));
                if (potion is not null)
                {
                    OfferRewards(RewardsSet.CreateCustom(
                        Owner,
                        potion: new PotionReward((PotionModel)potion.MutableClone(), Owner)));
                }
                break;
            case BattlewornRewardTier.Upgrades:
                List<CardModel> cards = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
                cards.StableShuffle(Rng);
                foreach (CardModel card in cards.Take(2)) CardCmd.Upgrade(card);
                break;
            case BattlewornRewardTier.Relic:
                RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner);
                OfferRewards(RewardsSet.CreateCustom(Owner, relic: new RelicReward((RelicModel)relic.MutableClone(), Owner)));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(rewardTier));
        }
    }

    private enum BattlewornRewardTier
    {
        Potion,
        Upgrades,
        Relic,
    }
}
