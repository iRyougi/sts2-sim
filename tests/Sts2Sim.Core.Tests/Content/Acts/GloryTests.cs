using System.Reflection;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Content.Acts;

public sealed class GloryTests
{
    [Fact]
    public void ScalarConfiguration_MatchesAuthoritativeGloryDefinition()
    {
        var act = new Glory();

        Assert.Equal(2, act.Index);
        Assert.Equal(13, act.BaseNumberOfRooms);
        Assert.Equal(2, act.NumberOfWeakEncounters);
    }

    [Fact]
    public void EventAndAncientPools_MatchAuthoritativeSourceOrder()
    {
        var act = new Glory();

        Assert.Equal(
            new[]
            {
                typeof(BattlewornDummy),
                typeof(GraveOfTheForgotten),
                typeof(HungryForMushrooms),
                typeof(Reflections),
                typeof(RoundTeaParty),
                typeof(Trial),
                typeof(TinkerTime),
            },
            act.EventPool);
        Assert.Equal(new[] { typeof(Nonupeipe), typeof(Tanx), typeof(Vakuu) }, act.AncientPool);
    }

    [Fact]
    public void EncounterPools_ContainAll18AuthoritativeEncountersInTheirExactCategoriesAndOrder()
    {
        var act = new Glory();

        Assert.Equal(
            new[]
            {
                "AxebotsNormal", "ConstructMenagerieNormal", "DevotedSculptorWeak", "FabricatorNormal",
                "FrogKnightNormal", "GlobeHeadNormal", "OwlMagistrateNormal",
                "ScrollsOfBitingNormal", "ScrollsOfBitingWeak",
                "SlimedBerserkerNormal", "TheLostAndForgottenNormal", "TurretOperatorWeak",
            },
            GetPool(act, "MonsterEncounters").Select(encounter => encounter.Name));
        Assert.Equal(
            new[] { "KnightsElite", "MechaKnightElite", "SoulNexusElite" },
            GetPool(act, "EliteEncounters").Select(encounter => encounter.Name));
        Assert.Equal(
            new[] { "AeonglassBoss", "QueenBoss", "TestSubjectBoss" },
            GetPool(act, "BossEncounters").Select(encounter => encounter.Name));

        Assert.Equal(18, GetPool(act, "MonsterEncounters").Count +
                         GetPool(act, "EliteEncounters").Count +
                         GetPool(act, "BossEncounters").Count);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(42UL)]
    [InlineData(987654321UL)]
    public void GetMapPointTypes_UsesGloryIntegerRestFormulaAndUnknownOffset(ulong seed)
    {
        var expectedRng = new Rng(seed);
        int expectedRests = expectedRng.NextInt(5, 7);
        int expectedUnknowns = MapPointTypeCounts.StandardRandomUnknownCount(expectedRng) - 1;

        MapPointTypeCounts actual = new Glory().GetMapPointTypes(new Rng(seed));

        Assert.Equal(expectedRests, actual.NumOfRests);
        Assert.Equal(expectedUnknowns, actual.NumOfUnknowns);
        Assert.InRange(actual.NumOfRests, 5, 6);
        Assert.InRange(actual.NumOfUnknowns, 9, 13);
        Assert.Equal(5, actual.NumOfElites);
        Assert.Equal(3, actual.NumOfShops);
    }

    private static IReadOnlyList<EncounterDefinition> GetPool(Glory act, string propertyName)
    {
        PropertyInfo property = typeof(ActDefinition).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return Assert.IsAssignableFrom<IReadOnlyList<EncounterDefinition>>(property.GetValue(act));
    }
}
