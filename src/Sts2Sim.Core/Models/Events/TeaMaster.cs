using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class TeaMaster : EventModel
{
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: < 2 } &&
        runState.Players.All(p => p.Gold >= 150);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new(Owner.Gold >= 50 ? "BONE_TEA" : "BONE_TEA_LOCKED", Owner.Gold >= 50 ? () => Buy<BoneTea>(50) : null),
        new(Owner.Gold >= 150 ? "EMBER_TEA" : "EMBER_TEA_LOCKED", Owner.Gold >= 150 ? () => Buy<EmberTea>(150) : null),
        new("TEA_OF_DISCOURTESY", () => Buy<TeaOfDiscourtesy>(0)),
    ];

    private async Task Buy<T>(int price) where T : RelicModel
    {
        if (price > 0) await PlayerCmd.LoseGold(price, Owner, GoldLossType.Spent);
        await RelicCmd.Obtain(ModelDb.Relic<T>(), Owner);
        Finish();
    }
}
