using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Events;

public sealed class Darv : AncientEventModel
{
    private static readonly (Type Relic, Func<int, bool> Filter)[] ValidRelicSets =
    [
        (typeof(Astrolabe), _ => true),
        (typeof(BlackStar), _ => true),
        (typeof(CallingBell), _ => true),
        (typeof(EmptyCage), _ => true),
        // #178: there is no ClearsPlayerDeck modifier in the headless run model.
        (typeof(PandorasBox), _ => true),
        (typeof(RunicPyramid), _ => true),
        (typeof(SneckoEye), _ => true),
        (typeof(Ectoplasm), act => act == 1),
        (typeof(Sozu), act => act == 1),
        (typeof(PhilosophersStone), act => act >= 1),
        (typeof(VelvetChoker), act => act >= 1),
    ];

    public override IReadOnlyList<RelicModel> AllPossibleOptions => ValidRelicSets.Select(set => set.Relic)
        .Append(typeof(DustyTome)).Select(type => (RelicModel)ModelDb.Get(type).MutableClone()).ToArray();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        int actIndex = ((Runs.RunState)RunState).CurrentActIndex;
        List<EventOption> options = ValidRelicSets.Where(set => set.Filter(actIndex))
            .Select(set => Option((RelicModel)ModelDb.Get(Rng.NextItem(new[] { set.Relic })!).MutableClone()))
            .ToList().UnstableShuffle(Rng);
        if (!Rng.NextBool()) return options.Take(3).ToArray();
        var tome = (DustyTome)ModelDb.Relic<DustyTome>().MutableClone();
        tome.SetupForPlayer(Owner);
        return options.Take(2).Append(Option(tome)).ToArray();
    }

    private EventOption Option(RelicModel relic) => new(relic.GetType().Name, async () =>
    {
        await RelicCmd.Obtain(relic, Owner);
        Finish();
    });
}
