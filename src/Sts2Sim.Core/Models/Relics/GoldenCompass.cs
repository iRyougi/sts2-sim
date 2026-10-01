using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
namespace Sts2Sim.Core.Models.Relics;

public sealed class GoldenCompass : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;
    public int GoldenPathAct { get; private set; } = -1;

    public override Task AfterObtained()
    {
        if (Owner.RunState is not RunState run)
            throw new InvalidOperationException("GoldenCompass requires a mutable run state.");
        GoldenPathAct = run.CurrentActIndex;
        run.RegenerateCurrentMap();
        return Task.CompletedTask;
    }

    public override ActMap ModifyGeneratedMap(IRunState runState, ActMap map, int actIndex) =>
        GoldenPathAct == actIndex ? new GoldenPathActMap(runState) : map;

    public override IReadOnlySet<RoomType> ModifyUnknownMapPointRoomTypes(IReadOnlySet<RoomType> roomTypes) =>
        Owner.RunState is RunState run && GoldenPathAct == run.CurrentActIndex
            ? new HashSet<RoomType> { RoomType.Event }
            : roomTypes;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(GoldenPathAct);
}
