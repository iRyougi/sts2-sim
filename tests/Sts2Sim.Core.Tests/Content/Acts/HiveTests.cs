using System.Reflection;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Content.Acts;

public sealed class HiveTests
{
    [Fact]
    public void ScalarConfiguration_MatchesAuthoritativeHiveDefinition()
    {
        var act = new Hive();

        Assert.Equal(1, act.Index);
        Assert.Equal(14, act.BaseNumberOfRooms);
        Assert.Equal(2, act.NumberOfWeakEncounters);
    }

    [Fact]
    public void EventAndAncientPools_MatchAuthoritativeSourceOrder()
    {
        var act = new Hive();

        Assert.Equal(
            new[]
            {
                typeof(Amalgamator),
                typeof(Bugslayer),
                typeof(ColorfulPhilosophers),
                typeof(ColossalFlower),
                typeof(FieldOfManSizedHoles),
                typeof(InfestedAutomaton),
                typeof(LostWisp),
                typeof(SpiritGrafter),
                typeof(TheLanternKey),
                typeof(ZenWeaver),
            },
            act.EventPool);
        Assert.Equal(new[] { typeof(Orobas), typeof(Pael), typeof(Tezcatara) }, act.AncientPool);
    }

    [Fact]
    public void EncounterPools_ContainAll20AuthoritativeEncountersInTheirExactCategories()
    {
        var act = new Hive();

        Assert.Equal(
            new[]
            {
                "BowlbugsNormal", "BowlbugsWeak", "ChompersNormal", "ExoskeletonsNormal",
                "ExoskeletonsWeak", "HunterKillerNormal", "LouseProgenitorNormal",
                "MytesNormal", "OvicopterNormal", "SlumberingBeetleNormal", "SpinyToadNormal", "TheObscuraNormal",
                "ThievingHopperWeak", "TunnelerWeak",
            },
            GetPool(act, "MonsterEncounters").Select(encounter => encounter.Name));
        Assert.Equal(
            new[] { "DecimillipedeElite", "EntomancerElite", "InfestedPrismsElite" },
            GetPool(act, "EliteEncounters").Select(encounter => encounter.Name));
        Assert.Equal(
            new[] { "KaiserCrabBoss", "KnowledgeDemonBoss", "TheInsatiableBoss" },
            GetPool(act, "BossEncounters").Select(encounter => encounter.Name));

        Assert.Equal(20, GetPool(act, "MonsterEncounters").Count +
                         GetPool(act, "EliteEncounters").Count +
                         GetPool(act, "BossEncounters").Count);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(987654321UL)]
    public void GetMapPointTypes_UsesHiveGaussianFormulas(ulong seed)
    {
        var expectedRng = new Rng(seed);
        int expectedRests = expectedRng.NextGaussianInt(6, 1, 6, 7);
        int expectedUnknowns = MapPointTypeCounts.StandardRandomUnknownCount(expectedRng) - 1;

        MapPointTypeCounts actual = new Hive().GetMapPointTypes(new Rng(seed));

        Assert.Equal(expectedRests, actual.NumOfRests);
        Assert.Equal(expectedUnknowns, actual.NumOfUnknowns);
        Assert.InRange(actual.NumOfRests, 6, 7);
        Assert.InRange(actual.NumOfUnknowns, 9, 13);
        Assert.Equal(5, actual.NumOfElites);
        Assert.Equal(3, actual.NumOfShops);
    }

    private static IReadOnlyList<EncounterDefinition> GetPool(Hive act, string propertyName)
    {
        PropertyInfo property = typeof(ActDefinition).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsAssignableFrom<IReadOnlyList<EncounterDefinition>>(property.GetValue(act));
    }
}
