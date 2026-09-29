using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Content;

public class OvergrowthTests
{
    [Fact]
    public void BaseNumberOfRooms_Is15_MatchingRealGameAct1()
    {
        Assert.Equal(15, new Overgrowth().BaseNumberOfRooms);
    }

    [Fact]
    public void NumberOfWeakEncounters_Is3_MatchingRealGameAct1()
    {
        Assert.Equal(3, new Overgrowth().NumberOfWeakEncounters);
    }

    [Fact]
    public void PickEncounter_FirstThreeMonsterRoomsUseWeakPool_ThenRegularPool()
    {
        var act = new Overgrowth();
        var rng = new Rng(42UL);

        for (int index = 0; index < act.BaseNumberOfRooms; index++)
        {
            EncounterDefinition encounter = act.PickEncounter(RoomType.Monster, rng);
            IReadOnlyList<(MonsterModel Monster, string? SlotName)> monsters =
                encounter.CreateMonsters(rng);

            Assert.Equal(index < act.NumberOfWeakEncounters, IsWeakEncounter(monsters));
        }
    }

    [Fact]
    public void GetMapPointTypes_UsesRealGaussianFormulas()
    {
        var act = new Overgrowth();
        MapPointTypeCounts counts = act.GetMapPointTypes(new Rng(1UL));

        Assert.InRange(counts.NumOfRests, 6, 7);
        Assert.InRange(counts.NumOfUnknowns, 10, 14);
        Assert.Equal(5, counts.NumOfElites);
        Assert.Equal(3, counts.NumOfShops);
    }

    [Fact]
    public void PickEncounter_ForMonsterRoomType_ReturnsRealOvergrowthMonsters()
    {
        var act = new Overgrowth();
        var rng = new Rng(2UL);
        EncounterDefinition encounter = act.PickEncounter(RoomType.Monster, rng);

        IReadOnlyList<(MonsterModel Monster, string? SlotName)> monsters = encounter.CreateMonsters(rng);
        Assert.NotEmpty(monsters);
        Assert.All(monsters, entry => Assert.True(entry.Monster.IsMutable));
        Assert.DoesNotContain(monsters, entry =>
            entry.Monster is WanderingGrunt or HardyBrute or ActOneGuardian);
    }

    private static bool IsWeakEncounter(
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> monsters)
    {
        if (monsters.Count == 1)
        {
            return monsters[0].Monster is FuzzyWurmCrawler or ShrinkerBeetle ||
                   monsters[0].Monster is Nibbit { IsAlone: true };
        }

        return monsters.Count == 3 &&
               monsters[0].Monster is LeafSlimeS or TwigSlimeS &&
               monsters[1].Monster is LeafSlimeM or TwigSlimeM &&
               monsters[2].Monster is LeafSlimeS or TwigSlimeS;
    }

    [Fact]
    public void PickEncounter_ForBossRoomType_ReturnsARealOvergrowthBoss()
    {
        var act = new Overgrowth();
        EncounterDefinition encounter = act.PickEncounter(RoomType.Boss, new Rng(3UL));

        MonsterModel monster = encounter.CreateMonster();
        Assert.Contains(monster.GetType(), new[] { typeof(CeremonialBeast), typeof(KinFollower), typeof(Vantom) });
        Assert.True(monster.IsMutable);
    }
}
