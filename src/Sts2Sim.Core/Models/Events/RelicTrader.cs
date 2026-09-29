using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class RelicTrader : EventModel
{
    public IReadOnlyList<RelicModel> OwnedRelics { get; private set; } = [];
    public IReadOnlyList<RelicModel> NewRelics { get; private set; } = [];
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: > 0 } &&
        runState.Players.All(p => p.Relics.Count(r => r.IsTradable) >= 5);

    protected override void CalculateVars()
    {
        var owned = Owner.Relics.Where(r => r.IsTradable).ToList();
        owned.Sort((a, b) => a.CompareTo(b));
        Rng.Shuffle(owned);
        OwnedRelics = owned.Take(3).ToArray();
        if (OwnedRelics.Count > 0)
            NewRelics = Enumerable.Range(0, 3).Select(_ => RelicFactory.PullNextRelicFromFront(Owner)).ToArray();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        string[] keys = ["TOP", "MIDDLE", "BOTTOM"];
        return OwnedRelics.Count == 0 ? [new("PROCEED", Done)] :
            Enumerable.Range(0, OwnedRelics.Count).Select(i => new EventOption(keys[i], () => Trade(i))).ToArray();
    }
    private async Task Trade(int index)
    {
        Owner.RemoveRelicInternal(OwnedRelics[index]);
        await OwnedRelics[index].AfterRemoved();
        await RelicCmd.Obtain(NewRelics[index], Owner);
        Finish();
    }
    private Task Done() { Finish(); return Task.CompletedTask; }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        OwnedRelics = new List<RelicModel>().AsReadOnly();
        NewRelics = new List<RelicModel>().AsReadOnly();
    }
}
