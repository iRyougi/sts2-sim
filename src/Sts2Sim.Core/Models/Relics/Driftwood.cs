using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Rewards;
namespace Sts2Sim.Core.Models.Relics;

public sealed class Driftwood : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool TryEnableCardRewardReroll(Player player, CardReward reward) =>
        ReferenceEquals(player, Owner);

    public override bool TryModifyCardRewardAlternatives(
        Player player, CardReward reward, List<CardRewardAlternative> alternatives)
    {
        if (!ReferenceEquals(player, Owner) || !reward.CanReroll)
            return false;
        alternatives.Add(new CardRewardAlternative("REROLL", reward.Reroll,
            () => reward.CanReroll && Owner.Relics.Contains(this), completesReward: false));
        return true;
    }
}
