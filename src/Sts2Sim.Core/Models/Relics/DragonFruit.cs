using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DragonFruit : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override Task AfterGoldGained(Player player) =>
        player == Owner ? CreatureCmd.GainMaxHp(Owner.Creature, 1m) : Task.CompletedTask;
}
