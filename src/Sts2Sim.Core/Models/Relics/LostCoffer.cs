using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LostCoffer : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        // Noncombat reward source uses base odds without modifying encounter rarity state.
        var cardReward = new CardReward(Owner,
            new CardCreationOptions([Owner.Character.CardPool], CardCreationSource.Other, CardRarityOddsType.RegularEncounter), 3);
        cardReward.Populate(Owner.RunState);

        var potionReward = new PotionReward(Owner);
        potionReward.Populate(Owner.RunState);

        OfferRewards(RewardsSet.CreateCustom(
            Owner,
            card: cardReward,
            potion: potionReward));
        return Task.CompletedTask;
    }
}
