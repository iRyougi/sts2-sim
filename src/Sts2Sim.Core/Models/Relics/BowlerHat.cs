using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BowlerHat : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override bool IsAllowedInShops => false;

    public override decimal ModifyGoldGained(Player player, decimal amount) =>
        player == Owner ? amount * 1.25m : amount;
}
