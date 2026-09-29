using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DreamCatcher : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool TryModifyRestSiteHealRewards(Player player, List<Reward> rewards, bool isMimicked)
    {
        if (player != Owner) return false;
        // Same options as upstream CardCreationOptions.ForRoom(player, RoomType.Monster).
        var options = new CardCreationOptions([player.Character.CardPool],
            CardCreationSource.Encounter, CardRarityOddsType.RegularEncounter);
        rewards.Add(new CardReward(Owner, options, 3));
        return true;
    }
}
