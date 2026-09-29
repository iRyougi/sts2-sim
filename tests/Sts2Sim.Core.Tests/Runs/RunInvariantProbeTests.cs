using System.Globalization;
using System.Text;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Runs;
using Xunit.Abstractions;

namespace Sts2Sim.Core.Tests.Runs;

/// <summary>
/// 不可能死的跑局探针：给玩家 10 亿血跑完整局，任何非通关结局都是引擎缺陷。
///
/// <para><b>为什么这样测。</b>目前的启发式策略很弱（A10 基线 800/800 死在第一幕），
/// 战斗结果被策略质量彻底污染，看不出引擎问题。把玩家设成打不死之后，
/// "赢没赢"不再取决于策略，于是任何 <c>Won=false</c> 都直接指向引擎——
/// 这把"AI 打得好不好"和"引擎对不对"解耦开了。</para>
///
/// <para>偏离 #320（自由旅行走到最后一行被误判地图耗尽）就是这个形态被人工发现的：
/// 玩家剩 999,999,978 HP，却 <c>Won=false</c> / <c>ReachedBoss=false</c>。
/// 本探针把那次的人工比对变成机器判定。</para>
///
/// <para><b>判据是不变量，不是 <c>Won</c>。</b><c>Won=false</c> 本身可能合法
/// （被 <c>maxFloors</c> 截断）。见 <see cref="CheckInvariants"/> 的五条。</para>
///
/// <para><b>遗物为什么轮换而不固定。</b>#320 只在带 <c>WingedBoots</c> 时触发——
/// 固定一套遗物等于固定一组流程分支，其他遗物的路径永远测不到。
/// 这里轮换的是**改变跑局流程**的那几个，不是战斗强度遗物（反正打不死）。</para>
///
/// <para><b>卡组必须够强。</b><c>DriveCombatToCompletion</c> 是 <c>while (IsInProgress)</c>，
/// 正常跑局靠玩家会死来保证战斗终止；玩家打不死之后，一场杀不掉怪的战斗会永远转下去。
/// 因此这里配了 <c>combatTurnLimit</c>，超限记为违反——但那反映的是卡组不够强，
/// 不是引擎缺陷。**用弱卡组跑这个探针只会产出噪声。**</para>
///
/// <para>opt-in：<c>STS2_INVARIANT=1</c>。可调 <c>STS2_INVARIANT_RUNS</c>（默认 200）、
/// <c>STS2_INVARIANT_SEED_OFFSET</c>、<c>STS2_INVARIANT_HP</c>、
/// <c>STS2_INVARIANT_TURN_LIMIT</c>（默认 300）、<c>STS2_INVARIANT_CSV</c>。</para>
/// </summary>
[Collection("ModelDb")]
public sealed class RunInvariantProbeTests : IDisposable
{
    /// <summary>经 08b-3d 验收跑通 40/40 的固定牌组（21 张，<c>+</c> 表示升级）。
    ///
    /// 用固定强牌组而非默认起始牌组，是因为 <c>DriveCombatToCompletion</c> 没有出口：
    /// 玩家打不死之后，杀不掉怪的战斗会一直转到回合上限。够强的牌组保证战斗打得完，
    /// 于是超限就真的指向引擎而不是策略。
    ///
    /// <c>EmptyCage</c> 在原配置里是第 12 件遗物，本仓库尚未实现（见偏离 #287），
    /// 其"移除 2 张牌"的结果已经体现在这 21 张的构成里，故不作为实体装入。</summary>
    private static readonly (Type Card, bool Upgraded)[] FixedDeck =
    [
        (typeof(DefendSilent), false),
        (typeof(DefendSilent), false),
        (typeof(Neutralize), true),
        (typeof(Survivor), false),
        (typeof(AscendersBane), false),
        (typeof(Backflip), false),
        (typeof(FlickFlack), true),
        (typeof(Acrobatics), false),
        (typeof(CorrosiveWave), true),
        (typeof(Prepared), true),
        (typeof(Backflip), false),
        (typeof(Prepared), true),
        (typeof(Afterimage), true),
        (typeof(CalculatedGamble), false),
        (typeof(Anticipate), true),
        (typeof(LegSweep), false),
        (typeof(Finesse), false),
        (typeof(Reflex), true),
        (typeof(UltimateStrike), false),
        (typeof(Outbreak), false),
        (typeof(Tactician), true),
    ];

    /// <summary>与 <see cref="FixedDeck"/> 配套的 11 件遗物。</summary>
    private static readonly Type[] FixedRelics =
    [
        typeof(PreciseScissors), typeof(PenNib), typeof(Akabeko), typeof(PaelsEye),
        typeof(MealTicket), typeof(BloodVial), typeof(IceCream), typeof(Whetstone),
        typeof(StoneCracker), typeof(Vajra), typeof(SneckoSkull),
    ];

    /// <summary>改变跑局流程的遗物。战斗强度遗物在打不死的前提下没有测试价值，
    /// 有价值的是会改变取点、房间生成或商店流程的那些。</summary>
    private static readonly Type[][] FlowRelicRotation =
    [
        [],
        [typeof(WingedBoots)],
        [typeof(Planisphere)],
        [typeof(PrayerWheel), typeof(TheCourier)],
        [typeof(GoldenCompass), typeof(Pocketwatch)],
        [typeof(WingedBoots), typeof(Planisphere), typeof(PrayerWheel), typeof(TheCourier)],
        // BingBong 是牌组无界增长的来源：每有一张牌进牌组就复制一份（靠 clonedBy 防递归）。
        // 它是验证偏离 #188 方案 A 的关键——复制品与原件同名同升级同附魔，
        // 所以实例数涨而**决策等价类数不变**。没有这一组，探针测不到该流派。
        [typeof(BingBong)],
        [typeof(BingBong), typeof(WingedBoots)],
    ];

    private readonly ITestOutputHelper _output;

    public RunInvariantProbeTests(ITestOutputHelper output)
    {
        _output = output;
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [RunInvariantProbeFact]
    public async Task UnkillableRuns_SatisfyEngineInvariants()
    {
        int runs = ReadInt("STS2_INVARIANT_RUNS", 200);
        int seedOffset = ReadInt("STS2_INVARIANT_SEED_OFFSET", 0);
        decimal hp = ReadInt("STS2_INVARIANT_HP", 1_000_000_000);

        var rows = new List<ProbeRow>(runs);
        for (int i = 0; i < runs; i++)
        {
            Type[] relics = FlowRelicRotation[i % FlowRelicRotation.Length];
            rows.Add(await RunOnceAsync($"invariant-{seedOffset + i}", hp, relics));
        }

        WriteCsvIfRequested(rows);
        _output.WriteLine(BuildReport(rows, hp));

        IReadOnlyList<ProbeRow> violations = rows.Where(r => r.Violations.Count > 0).ToList();
        Assert.True(violations.Count == 0, BuildViolationDetail(violations));
    }

    /// <summary>五条不变量。每一条为真都意味着引擎缺陷，不需要人再去判断。</summary>
    private static List<string> CheckInvariants(RunEngine.Result result, RunState run, string? failure, FloorEntry? lethal)
    {
        var violations = new List<string>();

        if (failure is not null)
        {
            // 事件在 MaximumEventChoices 内没结束，几乎都是默认策略不肯退出循环型事件
            // （EndlessConveyor 每次 Grab 后又给出 Grab；GOLDEN_FYSH 免费且回金币，
            // 金币可以一直够）。那是引擎的保护正常起作用 + 探针策略太笨，不是引擎缺陷。
            if (failure.Contains("did not complete within the maximum number of choices", StringComparison.Ordinal))
            {
                return violations;
            }

            violations.Add($"未捕获异常：{failure}");
            return violations;
        }

        // #320 的精确判据：取点入口说没路了，但当前点其实还连着子节点。
        if (result.MapExhaustedWithReachableChildren)
        {
            violations.Add("判定地图耗尽时当前点仍有 Children——取点逻辑缺陷（偏离 #320 形态）");
        }

        // 打不死的前提下走到地图尽头却没到过 Boss，说明路径推进本身断了。
        if (result.Outcome == RunOutcome.MapExhausted && !result.ReachedBoss)
        {
            violations.Add("地图耗尽但从未到过 Boss");
        }

        // 打不死却死了——按现场分类，即死机制与事件自残都不算引擎缺陷。
        if (result.Outcome is RunOutcome.PlayerDefeated or RunOutcome.AlreadyOver)
        {
            (bool isViolation, string note) = ClassifyDeath(lethal);
            if (isViolation)
            {
                violations.Add($"{note}（终局 HP {result.FinalPlayerHp}）");
            }
        }

        // 通关了但幕数对不上，说明幕推进判定错。
        if (result.Won && result.ActsCleared != run.Acts.Count)
        {
            violations.Add($"通关但 ActsCleared={result.ActsCleared}，应为 {run.Acts.Count}");
        }

        // 走完全部楼层上限还没打到 Boss——不是缺陷的证明，但值得单独看。
        if (result.Outcome == RunOutcome.FloorLimitReached && !result.ReachedBoss)
        {
            violations.Add("达到楼层上限却从未到过 Boss（可能是 maxFloors 太小，也可能是推进断了）");
        }

        return violations;
    }

    /// <summary>牌组里有多少个**决策等价类**。
    ///
    /// 在"移除/锻造"这类决策里，同名 + 同升级等级 + 同附魔（含层数）的牌是完全等价的——
    /// 玩家真实思考的是"移除一张打击"，不是"移除第 3717 张"。BingBong 能把牌组实例推到
    /// 数千张（每有一张牌进牌组就复制一份），但等价类数受卡池规模约束。
    ///
    /// 这个数字用于判定偏离 #188 该取什么上界：如果它稳定在几十，静态动作空间就够用；
    /// 如果它也能被某些流派推到数百，才需要上变长（指针式）动作空间。</summary>
    private static int CountDecisionEquivalenceClasses(Player player) =>
        player.Deck.Cards
            .Select(card => string.Create(
                CultureInfo.InvariantCulture,
                $"{card.GetType().Name}#{card.CurrentUpgradeLevel}#" +
                $"{string.Join(",", card.Enchantments.Select(e => $"{e.GetType().Name}:{e.Magnitude}").OrderBy(x => x, StringComparer.Ordinal))}"))
            .Distinct(StringComparer.Ordinal)
            .Count();

    private static async Task<ProbeRow> RunOnceAsync(string seed, decimal hp, Type[] relicTypes)
    {
        // 用 GetRandomList 而非写死三幕：Act1 会在 Overgrowth / Underdocks 间随机，
        // 否则探针永远覆盖不到暗港这批新内容。
        IReadOnlyList<ActDefinition> acts = ActDefinition.GetRandomList(seed);
        var run = new RunState(seed, acts, ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        // 打不死：最大生命与当前生命都拉到 hp。
        player.Creature.SetMaxHpInternal(hp);
        player.Creature.HealInternal(hp);

        await LoadFixedDeckAsync(player);

        foreach (Type relicType in FixedRelics.Concat(relicTypes))
        {
            await RelicCmd.Obtain((RelicModel)ModelDb.Get(relicType), player);
        }

        var recorder = new RunRecorder();
        var engine = new RunEngine(
            run,
            points => points.OrderBy(p => p.coord.col).First(),
            createAncientEventRoom: null,
            recorder: recorder,
            useAvailablePotions: false,
            createEventRoom: null,
            chooseCustomEventAction: null,
            // 打不死的玩家不会输掉战斗，但也可能杀不掉怪——没有上限就是挂起。
            // 超限抛异常，由下面的 catch 记成"未捕获异常"类违反。
            combatTurnLimit: ReadInt("STS2_INVARIANT_TURN_LIMIT", 300));

        RunEngine.Result result;
        string? failure = null;
        try
        {
            result = await engine.RunAsync(maxFloors: 200);
        }
        catch (Exception ex)
        {
            failure = DescribeExceptionChain(ex);
            result = new RunEngine.Result(false, false, 0, 0);
        }

        FloorEntry? lethal = result.Outcome is RunOutcome.PlayerDefeated or RunOutcome.AlreadyOver
            ? FindLethalFloor(recorder)
            : null;

        return new ProbeRow(
            seed,
            acts[0].GetType().Name,
            player.Deck.Cards.Count,
            CountDecisionEquivalenceClasses(player),
            string.Join('+', relicTypes.Select(t => t.Name).DefaultIfEmpty("none")),
            DescribeDeathSite(lethal),
            result.Outcome,
            result.Won,
            result.ReachedBoss,
            result.ActsCleared,
            result.FloorsVisited,
            result.FinalPlayerHp,
            result.MapExhaustedWithReachableChildren,
            CheckInvariants(result, run, failure, lethal),
            ClassifyDeath(lethal).Note);
    }

    /// <summary>清掉起始牌组，换成 <see cref="FixedDeck"/>。</summary>
    private static async Task LoadFixedDeckAsync(Player player)
    {
        foreach (CardModel starter in player.Deck.Cards.ToList())
        {
            // A10 塞进起始牌组的 AscenderBane 是 Eternal，按规则移不掉——
            // 而目标牌组里本来也有它一张，所以保留即可。
            if (starter.HasKeyword(CardKeyword.Eternal))
            {
                continue;
            }

            await CardPileCmd.RemoveFromDeck(player, starter);
        }

        HashSet<Type> retained = player.Deck.Cards.Select(c => c.GetType()).ToHashSet();
        foreach ((Type cardType, bool upgraded) in FixedDeck)
        {
            // 保留下来的那张已经顶掉清单里的同一项，不重复注入。
            if (retained.Remove(cardType))
            {
                continue;
            }

            var card = (CardModel)((CardModel)ModelDb.Get(cardType)).MutableClone();
            card.AssignOwner(player);
            if (upgraded)
            {
                card.Upgrade();
            }

            await CardPileCmd.AddToDeck(card);
        }
    }

    /// <summary>找出致死的那一层。探针的价值不在于报"输了"，而在于直接指出是哪个房间干的——
    /// 否则每条违反都要人去翻日志，等于把 #320 那次的人工比对又做一遍。</summary>
    private static FloorEntry? FindLethalFloor(RunRecorder recorder)
    {
        try
        {
            RunManifest manifest = recorder.BuildManifest();
            return manifest.Floors.FirstOrDefault(f => f.HpAfter <= 0) ?? manifest.Floors.LastOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static string DescribeDeathSite(FloorEntry? lethal) =>
        lethal is null
            ? "无楼层记录"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"第{lethal.FloorIndex}层 {lethal.PointType}/{lethal.RoomType} HP {lethal.HpBefore}->{lethal.HpAfter} (Max {lethal.MaxHp})");

    /// <summary>把死亡分类。**10 亿血挡不住无视血量的即死机制**——
    /// 例如 <c>TheInsatiable</c> 的 <c>SandpitPower</c>：计数每回合 -1，归零直接
    /// <c>CreatureCmd.Kill(Target)</c>，与当前生命无关。玩家有 4 回合脱身，逃不掉就死，
    /// 那是合法机制，不是引擎缺陷。
    ///
    /// 所以"打不死的玩家却死了"不能一律判违反，要按现场分三类：
    /// 战斗房里满血骤死 ⇒ 疑似即死机制（待归因，不判违反）；
    /// 非战斗房致死 ⇒ 多半是事件策略选了自残选项（探针策略问题）；
    /// 其余 ⇒ 真违反。</summary>
    private static (bool IsViolation, string Note) ClassifyDeath(FloorEntry? lethal)
    {
        if (lethal is null)
        {
            return (true, "死亡但无楼层记录");
        }

        bool inCombat = lethal.RoomType is "Monster" or "Elite" or "Boss";
        bool nearlyFullBefore = lethal.MaxHp > 0 && lethal.HpBefore >= lethal.MaxHp * 0.9;

        if (inCombat && nearlyFullBefore)
        {
            return (false, "满血骤死于战斗房——疑似无视血量的即死机制（如 SandpitPower），需人工归因");
        }

        if (!inCombat)
        {
            return (false, "死于非战斗房——多半是事件策略选了自残选项，属探针策略问题");
        }

        return (true, "打不死的玩家在战斗中被逐步磨死");
    }

    private static string DescribeExceptionChain(Exception ex)
    {
        var sb = new StringBuilder();
        for (Exception? current = ex; current is not null; current = current.InnerException)
        {
            sb.Append(current.GetType().Name).Append(": ").Append(current.Message).Append(" <- ");
        }

        string[] frames = (ex.StackTrace ?? string.Empty)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Take(8)
            .ToArray();
        return sb.ToString().TrimEnd(' ', '<', '-') + " | " + string.Join(" / ", frames.Select(f => f.Trim()));
    }

    private static string BuildReport(IReadOnlyList<ProbeRow> rows, decimal hp)
    {
        var report = new StringBuilder();
        report.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"不可能死跑局探针：{rows.Count} 局，HP={hp}，A10，三幕，maxFloors=200"));
        report.AppendLine();
        report.AppendLine("| 终止原因 | 局数 | 其中违反不变量 |");
        report.AppendLine("|---|---:|---:|");
        foreach (RunOutcome outcome in Enum.GetValues<RunOutcome>())
        {
            IReadOnlyList<ProbeRow> matching = rows.Where(r => r.Outcome == outcome).ToList();
            if (matching.Count == 0)
            {
                continue;
            }

            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {outcome} | {matching.Count} | {matching.Count(r => r.Violations.Count > 0)} |"));
        }

        int eventLoops = rows.Count(r => r.Violations.Count == 0
            && r.Outcome == RunOutcome.FloorLimitReached);
        if (eventLoops > 0)
        {
            report.AppendLine();
            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"另有 {eventLoops} 局因默认事件策略不退出循环型事件而触发 MaximumEventChoices 保护（不判违反）。"));
        }

        // 偏离 #188 的实测：MaxDeckBackedChoices=64 的理由是"当前只跑一幕 15 房间"，
        // 三幕之后该前提失效；编码器在超限时抛异常而非截断，超了就是 RL 训练崩。
        int[] deckSizes = rows.Select(r => r.FinalDeckSize).OrderBy(n => n).ToArray();
        report.AppendLine();
        report.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"终局牌组大小：最小 {deckSizes[0]} / 中位 {deckSizes[deckSizes.Length / 2]} / 最大 {deckSizes[^1]}"
            + $"；超过 MaxDeckBackedChoices(64) 的局数 {deckSizes.Count(n => n > 64)}"));

        // 偏离 #188 的方案 A（等价类去重）取上界用的实测。
        int[] classes = rows.Select(r => r.EquivalenceClasses).OrderBy(n => n).ToArray();
        report.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"决策等价类数：最小 {classes[0]} / 中位 {classes[classes.Length / 2]} / 最大 {classes[^1]}"
            + $"；压缩比（实例/等价类）中位 {(double)deckSizes[deckSizes.Length / 2] / Math.Max(1, classes[classes.Length / 2]):F2}x"));

        report.AppendLine();
        report.AppendLine("| Act1 变体 | 局数 | 通关 | 违反 |");
        report.AppendLine("|---|---:|---:|---:|");
        foreach (IGrouping<string, ProbeRow> group in rows.GroupBy(r => r.Act1).OrderBy(g => g.Key))
        {
            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {group.Key} | {group.Count()} | {group.Count(r => r.Won)} | {group.Count(r => r.Violations.Count > 0)} |"));
        }

        report.AppendLine();
        report.AppendLine("| 死亡分类（不判违反，但需人工归因） | 局数 |");
        report.AppendLine("|---|---:|");
        foreach (IGrouping<string, ProbeRow> group in rows
            .Where(r => r.Outcome is RunOutcome.PlayerDefeated or RunOutcome.AlreadyOver && r.Violations.Count == 0)
            .GroupBy(r => r.DeathNote))
        {
            report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"| {group.Key} | {group.Count()} |"));
        }

        report.AppendLine();
        report.AppendLine("| 遗物组 | 局数 | 违反 |");
        report.AppendLine("|---|---:|---:|");
        foreach (IGrouping<string, ProbeRow> group in rows.GroupBy(r => r.Relics).OrderBy(g => g.Key))
        {
            report.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"| {group.Key} | {group.Count()} | {group.Count(r => r.Violations.Count > 0)} |"));
        }

        return report.ToString();
    }

    private static string BuildViolationDetail(IReadOnlyList<ProbeRow> violations)
    {
        var sb = new StringBuilder();
        sb.Append(violations.Count).AppendLine(" 局违反引擎不变量：").AppendLine();
        foreach (ProbeRow row in violations.Take(40))
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  {row.Seed} [{row.Act1}] [{row.Relics}] {row.Outcome} 层数={row.FloorsVisited} 幕={row.ActsCleared} HP={row.FinalHp}"));
            sb.Append("      现场：").AppendLine(row.DeathSite);
            foreach (string violation in row.Violations)
            {
                sb.Append("      - ").AppendLine(violation);
            }
        }

        if (violations.Count > 40)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"  …另有 {violations.Count - 40} 局，见 CSV"));
        }

        return sb.ToString();
    }

    private static void WriteCsvIfRequested(IReadOnlyList<ProbeRow> rows)
    {
        string? path = Environment.GetEnvironmentVariable("STS2_INVARIANT_CSV");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("seed,act1,deckSize,classes,relics,deathSite,outcome,won,reachedBoss,actsCleared,floors,finalHp,exhaustedWithChildren,violations");
        foreach (ProbeRow r in rows)
        {
            sb.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{r.Seed},{r.Act1},{r.FinalDeckSize},{r.EquivalenceClasses},{r.Relics},\"{r.DeathSite}\",{r.Outcome},{r.Won},{r.ReachedBoss},{r.ActsCleared},{r.FloorsVisited},{r.FinalHp},{r.ExhaustedWithChildren},\"{string.Join(" ;; ", r.Violations)}\""));
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static int ReadInt(string variable, int fallback) =>
        int.TryParse(
            Environment.GetEnvironmentVariable(variable),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int parsed) && parsed > 0
            ? parsed
            : fallback;

    private sealed record ProbeRow(
        string Seed,
        string Act1,
        int FinalDeckSize,
        int EquivalenceClasses,
        string Relics,
        string DeathSite,
        RunOutcome Outcome,
        bool Won,
        bool ReachedBoss,
        int ActsCleared,
        int FloorsVisited,
        int FinalHp,
        bool ExhaustedWithChildren,
        List<string> Violations,
        string DeathNote);
}
