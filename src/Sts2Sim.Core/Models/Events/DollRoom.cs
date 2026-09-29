using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class DollRoom : EventModel
{
    private static List<RelicModel> Dolls() =>
        [ModelDb.Relic<DaughterOfTheWind>(), ModelDb.Relic<MrStruggles>(), ModelDb.Relic<BingBong>()];
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: 1 };
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("RANDOM", () => Take(Rng.NextItem(Dolls())!)),
         new("TAKE_SOME_TIME", () => Examine(5, 2)), new("EXAMINE", () => Examine(15, 3))];

    private async Task Examine(int damage, int choices)
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, damage, ValueProp.Unblockable | ValueProp.Unpowered);
        var dolls = Dolls();
        dolls.Sort((a, b) => a.CompareTo(b));
        Rng.Shuffle(dolls);
        SetOptions(dolls.Take(choices).Select(relic => new EventOption(relic.Id.Entry, () => Take(relic))).ToArray());
    }

    private async Task Take(RelicModel relic)
    {
        await RelicCmd.Obtain(relic, Owner);
        Finish();
    }
}
