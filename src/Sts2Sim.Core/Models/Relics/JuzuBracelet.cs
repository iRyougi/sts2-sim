using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class JuzuBracelet : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(
        IReadOnlySet<RoomType> roomTypes)
    {
        var result = new HashSet<RoomType>(roomTypes);
        result.Remove(RoomType.Monster);
        result.Remove(RoomType.Elite); // v0.113.1
        return result;
    }
}
