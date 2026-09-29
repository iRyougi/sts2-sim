using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class WhiteBeastStatue : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override bool ShouldForcePotionReward(RoomType roomType) =>
        roomType is RoomType.Monster or RoomType.Elite or RoomType.Boss;
}
