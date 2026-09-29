namespace Sts2Sim.Core.Content.Acts;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;

public static partial class UnderdocksEncounters
{
    public static IReadOnlyList<EncounterDefinition> Elites { get; } =
    [
        new(CreatePhantasmalGardeners, null, false, "PhantasmalGardenersElite"),
        new(() => [(EliteMonster<SkulkingColony>(), null)], null, false, "SkulkingColonyElite"),
        new(() => [(EliteMonster<TerrorEel>(), null)], null, false, "TerrorEelElite"),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreatePhantasmalGardeners() =>
    [
        (EliteMonster<PhantasmalGardener>(), "first"),
        (EliteMonster<PhantasmalGardener>(), "second"),
        (EliteMonster<PhantasmalGardener>(), "third"),
        (EliteMonster<PhantasmalGardener>(), "fourth"),
    ];

    private static T EliteMonster<T>() where T : MonsterModel => ModelDb.Monster<T>();
}
