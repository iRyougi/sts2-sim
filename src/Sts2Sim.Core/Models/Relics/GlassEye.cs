using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GlassEye : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override Task AfterObtained()
    {
        var rewards = new List<Reward>();
        foreach (CardRarity rarity in new[]
                 {
                     CardRarity.Common, CardRarity.Common,
                     CardRarity.Uncommon, CardRarity.Uncommon, CardRarity.Rare,
                 })
        {
            CardCreationOptions options = CardCreationOptions.ForNonCombatWithUniformOdds(
                    [Owner.Character.CardPool], card => card.Rarity == rarity)
                .WithFlags(CardCreationFlags.NoRarityModification);
            rewards.Add(new CardReward(Owner, options, 3));
        }

        return RewardsCmd.OfferCustom(Owner, rewards);
    }
}
