using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Grants 15 extra gold after combat victories.</summary>
public sealed class AmethystAubergine : RelicModel
{
    private const int GoldGranted = 15;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override bool IsAllowedInShops => false;

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (player != Owner || !roomType.IsCombatRoom())
        {
            return;
        }

        var gold = new GoldReward(GoldGranted, player);
        rewards.Add(gold);
    }
}
