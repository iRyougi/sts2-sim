using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class MultiplayerCombatRewardIntegrationTests : IDisposable
{
    public MultiplayerCombatRewardIntegrationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunEngine_BossCombat_ResolvesNormalRewardsForBothPlayersAndIndexOneLavaExtras()
    {
        RewardRun context = await CreateRunAsync("multiplayer-lava-engine");
        var engine = new RunEngine(context.RunState, PickFirstByCoord);
        RewardSnapshot? afterBoss = null;
        engine.OnRoomResolved += (_, roomType) =>
        {
            if (roomType == RoomType.Boss)
            {
                afterBoss = RewardSnapshot.Capture(context);
            }
        };

        await engine.RunAsync(maxFloors: 1);

        AssertRewardResolution(context, afterBoss);
    }

    [Fact]
    public async Task RunDriver_BossCombat_ResolvesNormalRewardsForBothPlayersAndIndexOneLavaExtras()
    {
        RewardRun context = await CreateRunAsync("multiplayer-lava-driver");
        var driver = new RunDriver(context.RunState, new DefaultDecisionSource());
        RewardSnapshot? afterBoss = null;
        driver.OnRoomResolved += (_, roomType) =>
        {
            if (roomType == RoomType.Boss)
            {
                afterBoss = RewardSnapshot.Capture(context);
            }
        };

        await driver.RunAsync(maxFloors: 1);

        AssertRewardResolution(context, afterBoss);
    }

    private static async Task<RewardRun> CreateRunAsync(string seed)
    {
        // 偏离 #311：最终幕 Boss 不发奖励，故本夹具须为多幕，让 Boss 场次不是最终幕。
        var runState = new RunState(seed, [new Overgrowth(), new Hive()]);
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        Player first = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(first);
        runState.AddPlayer(owner);
        first.Creature.SetMaxHpInternal(10_000m);
        first.Creature.HealInternal(10_000m);
        owner.Creature.SetMaxHpInternal(10_000m);
        owner.Creature.HealInternal(10_000m);
        PickFirstByCoord(runState.Map.StartingMapPoint.Children).PointType = MapPointType.Boss;
        await RelicCmd.Obtain(ModelDb.Relic<LavaRock>(), owner);
        LavaRock lavaRock = Assert.Single(owner.Relics.OfType<LavaRock>());
        return new RewardRun(
            runState,
            first,
            owner,
            lavaRock,
            first.Gold,
            owner.Gold,
            first.Deck.Cards.Count,
            owner.Deck.Cards.Count,
            first.Relics.Count,
            owner.Relics.Count);
    }

    /// <summary>偏离 #311 使本夹具改为双幕（否则这场 Boss 就是最终幕 Boss，按权威不发奖励）。
    /// 双幕后引擎打完 Boss 会推进第二幕并进入其起点的先古之民房，那里会再给卡与遗物——
    /// 所以断言必须读"Boss 房刚结算完"的快照，而不是整趟跑完的终值。</summary>
    private static void AssertRewardResolution(RewardRun context, RewardSnapshot? afterBoss)
    {
        Assert.NotNull(afterBoss);
        Assert.True(context.LavaRock.HasTriggered);
        Assert.True(afterBoss.FirstGold > context.FirstGoldBefore);
        Assert.True(afterBoss.OwnerGold > context.OwnerGoldBefore);
        Assert.Equal(context.FirstDeckBefore + 1, afterBoss.FirstDeck);
        Assert.Equal(context.OwnerDeckBefore + 1, afterBoss.OwnerDeck);
        Assert.Equal(context.FirstRelicsBefore, afterBoss.FirstRelics);
        Assert.Equal(context.OwnerRelicsBefore + 2, afterBoss.OwnerRelics);
    }

    private static MapPoint PickFirstByCoord(IEnumerable<MapPoint> points) =>
        points.OrderBy(point => point.coord.col).First();

    private sealed class DefaultDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(PickFirstByCoord(options));

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            Player player = state.Players[0];
            CardModel? playable = player.PlayerCombatState!.Hand.Cards
                .FirstOrDefault(card => card.CanPlay(out _));
            if (playable is null)
            {
                return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
            }

            Creature? target = playable.TargetType == TargetType.AnyEnemy
                ? state.HittableEnemies.FirstOrDefault()
                : null;
            return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(playable, target));
        }
    }

    private sealed record RewardSnapshot(
        int FirstGold,
        int OwnerGold,
        int FirstDeck,
        int OwnerDeck,
        int FirstRelics,
        int OwnerRelics)
    {
        public static RewardSnapshot Capture(RewardRun context) => new(
            context.First.Gold,
            context.Owner.Gold,
            context.First.Deck.Cards.Count,
            context.Owner.Deck.Cards.Count,
            context.First.Relics.Count,
            context.Owner.Relics.Count);
    }

    private sealed record RewardRun(
        RunState RunState,
        Player First,
        Player Owner,
        LavaRock LavaRock,
        int FirstGoldBefore,
        int OwnerGoldBefore,
        int FirstDeckBefore,
        int OwnerDeckBefore,
        int FirstRelicsBefore,
        int OwnerRelicsBefore);
}
