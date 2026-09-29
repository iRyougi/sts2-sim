using Sts2Sim.Core.Content;

namespace Sts2Sim.Core.Runs;

/// <summary>Per-run room generation results. Visiting later acts consumes no UpFront RNG.</summary>
internal sealed record GeneratedActRooms(
    List<Type> Events,
    List<EncounterDefinition> NormalEncounters,
    List<EncounterDefinition> EliteEncounters,
    EncounterDefinition Boss,
    EncounterDefinition? SecondBoss,
    Type Ancient);
