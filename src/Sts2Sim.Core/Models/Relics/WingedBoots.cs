using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class WingedBoots : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public int TimesUsed { get; private set; }

    public override bool IsUsedUp => TimesUsed >= 3;

    public override bool IsAllowed(IRunState runState) => runState.Players.Count == 1;

    public override bool ShouldAllowFreeTravel() => !IsUsedUp;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (IsUsedUp || Owner.RunState is not RunState runState ||
            runState.CurrentRoomCount > 1 || runState.VisitedMapCoords.Count <= 1)
            return Task.CompletedTask;

        MapPoint? previous = runState.Map.GetPoint(runState.VisitedMapCoords[^2]);
        MapPoint? current = runState.CurrentMapPoint;
        if (previous is null || current is null || previous.Children.Contains(current))
            return Task.CompletedTask;

        TimesUsed++;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(TimesUsed);
    }
}
