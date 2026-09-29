using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Models.Encounters;

// Inherited deviation #46: event encounters use EncounterDefinition instead of ModelDb models.
public static class PunchOffEventEncounter
{
    public static EncounterDefinition Definition => new(
        rng =>
        {
            var first = (PunchConstruct)ModelDb.Monster<PunchConstruct>().MutableClone();
            first.StartsWithFastPunch = true;
            first.StartingHpReduction = rng.NextInt(2, 10);
            var second = (PunchConstruct)ModelDb.Monster<PunchConstruct>().MutableClone();
            second.StartingHpReduction = rng.NextInt(2, 10);
            return new[] { ((MonsterModel)first, (string?)null), ((MonsterModel)second, (string?)null) };
        },
        tags: null, isWeak: false, name: "PunchOffEventEncounter");
}
