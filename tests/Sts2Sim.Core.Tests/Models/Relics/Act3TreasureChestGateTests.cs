using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

/// <summary>
/// 偏离 #108：真实门控是 IsBeforeAct3TreasureChest —— 单人 TotalFloor &lt; 41。
/// 不是 CurrentActIndex &lt; 2。过了第三幕宝箱再发这类"投资型"遗物等于发空气。
/// </summary>
[Collection("ModelDb")]
public sealed class Act3TreasureChestGateTests : IDisposable
{
    public Act3TreasureChestGateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<Type> GatedRelics =>
    [
        typeof(AmethystAubergine), typeof(BookOfFiveRings), typeof(BowlerHat),
        typeof(DragonFruit), typeof(JuzuBracelet), typeof(LuckyFysh),
        typeof(MealTicket), typeof(OldCoin), typeof(Planisphere),
        typeof(WhiteBeastStatue), typeof(WhiteStar),
    ];

    [Theory]
    [MemberData(nameof(GatedRelics))]
    public void GatedRelic_IsAllowed_BeforeSinglePlayerThreshold(Type relicType)
    {
        RunState runState = CreateRunAtFloor("gate-before", 40);

        Assert.True(ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType)).IsAllowed(runState));
    }

    [Theory]
    [MemberData(nameof(GatedRelics))]
    public void GatedRelic_IsNotAllowed_AtSinglePlayerThreshold(Type relicType)
    {
        RunState runState = CreateRunAtFloor("gate-at", 41);

        Assert.False(ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType)).IsAllowed(runState));
    }

    [Theory]
    [MemberData(nameof(GatedRelics))]
    public void GatedRelic_IsAllowed_BeforeMultiplayerThreshold(Type relicType)
    {
        RunState runState = CreateRunAtFloor("gate-multiplayer-before", 37, playerCount: 2);

        Assert.True(ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType)).IsAllowed(runState));
    }

    [Theory]
    [MemberData(nameof(GatedRelics))]
    public void GatedRelic_IsNotAllowed_AtMultiplayerThreshold(Type relicType)
    {
        RunState runState = CreateRunAtFloor("gate-multiplayer-at", 38, playerCount: 2);

        Assert.False(ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType)).IsAllowed(runState));
    }

    [Fact]
    public void UngatedRelic_IgnoresTheThreshold()
    {
        RunState runState = CreateRunAtFloor("gate-ungated", 41);

        Assert.True(ModelDb.GetById<RelicModel>(ModelDb.GetId(typeof(Vajra))).IsAllowed(runState));
    }

    /// <summary>把 run 推到指定 TotalFloor。用 col 递增造出互不相同的 MapCoord，
    /// 因为 AddVisitedMapCoord 对重复坐标是幂等的。</summary>
    private static RunState CreateRunAtFloor(string seed, int totalFloor, int playerCount = 1)
    {
        var runState = new RunState(seed, new ActDefinition[] { new Overgrowth(), new Hive() });
        for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
        {
            Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
            runState.AddPlayer(player);
        }
        for (int index = 0; index < totalFloor; index++)
        {
            runState.AddVisitedMapCoord(new MapCoord(index, 0));
        }

        return runState;
    }
}
