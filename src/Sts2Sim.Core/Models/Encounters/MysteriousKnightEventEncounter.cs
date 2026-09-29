using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Models.Encounters;

public static class MysteriousKnightEventEncounter
{
    public static EncounterDefinition Definition => new(
        () => new[]
        {
            ((MonsterModel)ModelDb.Monster<MysteriousKnight>().MutableClone(), (string?)null),
        },
        tags: null,
        isWeak: false,
        name: "MysteriousKnightEventEncounter");
}
