using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Adds a boss-pool card reward after elite combats.</summary>
public sealed class WhiteStar : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (player != Owner || roomType != RoomType.Elite)
        {
            return;
        }

        var cards = new CardReward(player, CardRarityOddsType.BossEncounter);
        cards.Populate(player.RunState);
        rewards.Add(cards);
    }
}
