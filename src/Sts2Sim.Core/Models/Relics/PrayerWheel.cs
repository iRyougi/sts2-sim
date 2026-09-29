using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Adds a regular-encounter card reward after normal monster combats.</summary>
public sealed class PrayerWheel : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (player != Owner || roomType != RoomType.Monster)
        {
            return;
        }

        var cards = new CardReward(player, CardRarityOddsType.RegularEncounter);
        cards.Populate(player.RunState);
        rewards.Add(cards);
    }
}
