using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Demo;

/// <summary>
/// console 全局回放 demo 的核心逻辑（脱离 Console，方便单元测试）：储君从开局跑到 Act 1 Boss，
/// "随机策略"选点，固定策略"手牌里能打的牌全部打出"（与 Plan 03 <c>CombatReplayDemo</c> 相同的出牌
/// 策略）。这是 Plan 04 要求的可见成果任务。
/// </summary>
public static class RunReplayDemo
{
    public const int MaxFloors = 60;

    public sealed record Result(bool Won, int FloorsVisited, int FinalPlayerHp);

    public static void EnsureModelsRegistered() => ModelDb.Init(ContentRegistry.AllTypes);

    public static async Task<Result> RunAsync(string seed, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        EnsureModelsRegistered();

        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        HashSet<Type> knownRelicTypes = player.Relics
            .Select(relic => relic.GetType())
            .ToHashSet();

        // 偏离 #50：policy rng 独立于 RunRngSet 的任何流，只用于 demo 的"随机策略"选点决策。
        var policySeededRng = new Sts2Sim.Core.Random.Rng(StringHelper.GetDeterministicHashCode("policy:" + seed));

        var engine = new RunEngine(runState, points => policySeededRng.NextItem(points)!);
        engine.OnRoomResolved += (point, roomType) =>
        {
            string[] acquiredRelics = player.Relics
                .Select(relic => relic.GetType())
                .Where(knownRelicTypes.Add)
                .Select(relicType => relicType.Name)
                .ToArray();
            string acquiredRelicSummary = acquiredRelics.Length == 0
                ? string.Empty
                : $" | new relic: {string.Join(", ", acquiredRelics)}";
            log($"[floor {runState.VisitedMapCoords.Count}] ({point.coord.col},{point.coord.row}) {point.PointType} -> {roomType}: " +
                DescribeOutcome(roomType, player) +
                acquiredRelicSummary);
        };

        log($"=== Sts2Sim run 回放 demo (seed=\"{seed}\") ===");
        RunEngine.Result result = await engine.RunAsync(MaxFloors);

        log(string.Empty);
        log(result.Won ? "=== Act 1 Boss 击败，run 胜利！===" : "=== run 失败或未在层数上限内到达 Boss ===");
        log($"共访问 {result.FloorsVisited} 层。玩家剩余 HP：{result.FinalPlayerHp}/{player.Creature.MaxHp}");

        return new Result(result.Won, result.FloorsVisited, result.FinalPlayerHp);
    }

    private static string DescribeOutcome(RoomType roomType, Player player)
    {
        return roomType switch
        {
            RoomType.Monster or RoomType.Elite or RoomType.Boss =>
                $"战斗结束，玩家 HP {player.Creature.CurrentHp}/{player.Creature.MaxHp}，牌组 {player.Deck.Cards.Count} 张，最近入组 {player.Deck.Cards.Last().GetType().Name}",
            RoomType.RestSite =>
                $"休息点结算，玩家 HP {player.Creature.CurrentHp}/{player.Creature.MaxHp}，已升级 {player.Deck.Cards.Count(card => card.IsUpgraded)} 张",
            RoomType.Treasure => $"获得金币，当前金币 {player.Gold}",
            RoomType.Shop => "商店库存已生成，自动策略直接离开",
            RoomType.Event => "Event resolved",
            _ => "无操作",
        };
    }
}
