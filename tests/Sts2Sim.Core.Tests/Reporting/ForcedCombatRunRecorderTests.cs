using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Reporting;

file sealed class ReportingForcedCombatEvent : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("FIGHT", () =>
        {
            RequestForcedCombat(() =>
                (MonsterModel)ModelDb.Monster<ReportingForcedCombatMonster>().MutableClone());
            Finish();
            return Task.CompletedTask;
        }),
    ];
}

file sealed class ReportingThrowingForcedCombatEvent : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("FIGHT", () =>
        {
            RequestForcedCombat(() =>
                (MonsterModel)ModelDb.Monster<ReportingThrowingForcedCombatMonster>().MutableClone());
            Finish();
            return Task.CompletedTask;
        }),
    ];
}

file sealed class ReportingForcedCombatMonster : MonsterModel
{
    public override int MinInitialHp => 1;

    public override int MaxInitialHp => 1;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("WAIT", _ => Task.CompletedTask, new DefendIntent());
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }
}

file sealed class ReportingThrowingForcedCombatMonster : MonsterModel
{
    public override int MinInitialHp => 999;

    public override int MaxInitialHp => 999;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState(
            "THROW",
            _ => throw new InvalidOperationException("Forced combat move failed."));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }
}

[Collection("ModelDb")]
public sealed class ForcedCombatRunRecorderTests : IDisposable
{
    public ForcedCombatRunRecorderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(
        [
            typeof(ReportingForcedCombatEvent),
            typeof(ReportingThrowingForcedCombatEvent),
            typeof(ReportingForcedCombatMonster),
            typeof(ReportingThrowingForcedCombatMonster),
        ]));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_AncientForcedCombatRecordsLogAlongsideAncientFloorWithoutCombatRewards(
        bool useDriver)
    {
        (RunState runState, Player player) = CreateAncientRun($"forced-recorder-success-{useDriver}");
        int goldBefore = player.Gold;
        string[] deckBefore = player.Deck.Cards.Select(card => card.Id.ToString()).ToArray();
        string?[] relicsBefore = player.Relics.Select(relic => relic.Id.ToString()).ToArray();
        string?[] potionsBefore = player.PotionSlots.Select(potion => potion?.Id.ToString()).ToArray();
        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);

        await RunAncientAsync(
            useDriver,
            runState,
            recorder,
            () => (EventModel)ModelDb.Event<ReportingForcedCombatEvent>().MutableClone());

        RunManifest manifest = recorder.BuildManifest();
        FloorEntry floor = Assert.Single(manifest.Floors);
        CombatLog log = Assert.Single(recorder.CombatLogs).Value;

        CombatLogRef combatRef = Assert.Single(manifest.CombatLogs);
        Assert.Equal(log.CombatId, combatRef.CombatId);
        Assert.Equal(floor.FloorIndex, combatRef.Floor);
        Assert.Equal($"combats/{floor.FloorIndex:D3}_{log.CombatId}.json", combatRef.CombatLogFile);
        Assert.Equal(MapPointType.Ancient.ToString(), floor.PointType);
        Assert.Equal(RoomType.Event.ToString(), floor.RoomType);
        AncientFloorDetail detail = Assert.IsType<AncientFloorDetail>(floor.Detail);
        Assert.Equal(ModelDb.Event<ReportingForcedCombatEvent>().GetType().Name, detail.EventName);
        Assert.Equal("FIGHT", detail.OptionChosen);
        Assert.Equal(floor.FloorIndex, log.Floor);
        Assert.Equal(RoomType.Monster.ToString(), log.EncounterType);
        Assert.Equal(CombatRoom.ForcedEncounterName, log.EncounterName);
        Assert.Equal("victory", log.Result);
        Assert.Equal(0, log.Rewards.Gold);
        Assert.Empty(log.Rewards.CardsOffered);
        Assert.Empty(log.Rewards.CardsTaken);
        Assert.Empty(log.Rewards.RelicsTaken);
        Assert.Empty(log.Rewards.PotionsTaken);
        Assert.Null(log.Rewards.CardTaken);
        Assert.Null(log.Rewards.RelicTaken);
        Assert.Null(log.Rewards.PotionTaken);
        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(deckBefore, player.Deck.Cards.Select(card => card.Id.ToString()));
        Assert.Equal(relicsBefore, player.Relics.Select(relic => relic.Id.ToString()));
        Assert.Equal(potionsBefore, player.PotionSlots.Select(potion => potion?.Id.ToString()));
        Assert.Equal(floor.DeckBefore, floor.DeckAfter);
        Assert.Equal(floor.RelicsBefore, floor.RelicsAfter);
        Assert.Equal(floor.PotionsBefore, floor.PotionsAfter);
        Assert.Null(runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Null(player.Creature.CombatState);
        string markdown = MarkdownReportRenderer.Render(manifest, recorder.CombatLogs);
        Assert.Contains("ancient=", markdown, StringComparison.Ordinal);
        Assert.Contains(CombatRoom.ForcedEncounterName, markdown, StringComparison.Ordinal);

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_AncientForcedCombatMoveFailurePreservesExceptionAndTearsDown(
        bool useDriver)
    {
        (RunState runState, Player player) = CreateAncientRun($"forced-recorder-throw-{useDriver}");
        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAncientAsync(
                useDriver,
                runState,
                recorder,
                () => (EventModel)ModelDb.Event<ReportingThrowingForcedCombatEvent>().MutableClone()));

        Assert.Equal("Forced combat move failed.", exception.Message);
        Assert.Null(runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Null(player.Creature.CombatState);
        Assert.Throws<InvalidOperationException>(recorder.BuildManifest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_EventForcedCombatRecordsLogAlongsideEventFloorWithoutCombatRewards(
        bool useDriver)
    {
        (RunState runState, Player player) = CreateEventRun($"forced-recorder-event-success-{useDriver}");
        int goldBefore = player.Gold;
        string[] deckBefore = player.Deck.Cards.Select(card => card.Id.ToString()).ToArray();
        string?[] relicsBefore = player.Relics.Select(relic => relic.Id.ToString()).ToArray();
        string?[] potionsBefore = player.PotionSlots.Select(potion => potion?.Id.ToString()).ToArray();
        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);

        await RunEventAsync(
            useDriver,
            runState,
            recorder,
            () => (EventModel)ModelDb.Event<ReportingForcedCombatEvent>().MutableClone());

        RunManifest manifest = recorder.BuildManifest();
        FloorEntry floor = Assert.Single(manifest.Floors);
        CombatLog log = Assert.Single(recorder.CombatLogs).Value;

        CombatLogRef combatRef = Assert.Single(manifest.CombatLogs);
        Assert.Equal(log.CombatId, combatRef.CombatId);
        Assert.Equal(floor.FloorIndex, combatRef.Floor);
        Assert.Equal($"combats/{floor.FloorIndex:D3}_{log.CombatId}.json", combatRef.CombatLogFile);
        Assert.Equal(MapPointType.Unknown.ToString(), floor.PointType);
        Assert.Equal(RoomType.Event.ToString(), floor.RoomType);
        EventFloorDetail detail = Assert.IsType<EventFloorDetail>(floor.Detail);
        Assert.Equal(ModelDb.Event<ReportingForcedCombatEvent>().GetType().Name, detail.EventName);
        Assert.Equal("FIGHT", detail.OptionChosen);
        Assert.Equal(floor.FloorIndex, log.Floor);
        Assert.Equal(RoomType.Monster.ToString(), log.EncounterType);
        Assert.Equal(CombatRoom.ForcedEncounterName, log.EncounterName);
        Assert.Equal("victory", log.Result);
        Assert.Equal(0, log.Rewards.Gold);
        Assert.Empty(log.Rewards.CardsOffered);
        Assert.Empty(log.Rewards.CardsTaken);
        Assert.Empty(log.Rewards.RelicsTaken);
        Assert.Empty(log.Rewards.PotionsTaken);
        Assert.Null(log.Rewards.CardTaken);
        Assert.Null(log.Rewards.RelicTaken);
        Assert.Null(log.Rewards.PotionTaken);
        Assert.Equal(goldBefore, player.Gold);
        Assert.Equal(deckBefore, player.Deck.Cards.Select(card => card.Id.ToString()));
        Assert.Equal(relicsBefore, player.Relics.Select(relic => relic.Id.ToString()));
        Assert.Equal(potionsBefore, player.PotionSlots.Select(potion => potion?.Id.ToString()));
        Assert.Equal(floor.DeckBefore, floor.DeckAfter);
        Assert.Equal(floor.RelicsBefore, floor.RelicsAfter);
        Assert.Equal(floor.PotionsBefore, floor.PotionsAfter);
        Assert.Null(runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Null(player.Creature.CombatState);
        string markdown = MarkdownReportRenderer.Render(manifest, recorder.CombatLogs);
        Assert.Contains("event=", markdown, StringComparison.Ordinal);
        Assert.Contains(CombatRoom.ForcedEncounterName, markdown, StringComparison.Ordinal);

    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_EventForcedCombatMoveFailurePreservesExceptionAndTearsDown(
        bool useDriver)
    {
        (RunState runState, Player player) = CreateEventRun($"forced-recorder-event-throw-{useDriver}");
        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunEventAsync(
                useDriver,
                runState,
                recorder,
                () => (EventModel)ModelDb.Event<ReportingThrowingForcedCombatEvent>().MutableClone()));

        Assert.Equal("Forced combat move failed.", exception.Message);
        Assert.Null(runState.CurrentRoom);
        Assert.Null(player.PlayerCombatState);
        Assert.Null(player.Creature.CombatState);
        Assert.Throws<InvalidOperationException>(recorder.BuildManifest);
    }

    private static (RunState RunState, Player Player) CreateEventRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        foreach (MapPoint child in runState.Map.StartingMapPoint.Children)
        {
            child.PointType = MapPointType.Unknown;
        }

        // 本测试需要 ? 点确定地解析成事件房间。原先靠 MockRng 达成，但那句其实一直是无效的：
        // RunState 构造时把 Rng 实例引用交给了 Odds（RunState.cs:129），而 MockRng 只替换
        // RunRngSet 字典里的条目，动不到已持有的引用。偏离 #327 删掉首局强制 Event 分支之前，
        // ? 点根本不 roll，这个失效点一直被掩盖着。
        // 改为显式关掉全部非事件赔率：Roll 会跳过 odds < 0 的房型，保留 Event 兜底，
        // 且照常消耗一次 RNG——确定性来自赔率，不来自种子运气。
        runState.Odds.UnknownMapPoint.MonsterOdds = -1f;
        runState.Odds.UnknownMapPoint.TreasureOdds = -1f;
        runState.Odds.UnknownMapPoint.ShopOdds = -1f;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static async Task RunEventAsync(
        bool useDriver,
        RunState runState,
        IRunRecorder recorder,
        Func<EventModel> createEvent)
    {
        Func<RunState, AbstractRoom> createEventRoom = _ => new EventRoom(createEvent);
        if (useDriver)
        {
            await new RunDriver(
                runState,
                new FirstActionDecisionSource(),
                createAncientEventRoom: null,
                recorder: recorder,
                useAvailablePotions: false,
                createEventRoom: createEventRoom).RunAsync(maxFloors: 1);
            return;
        }

        await new RunEngine(
            runState,
            points => points.OrderBy(point => point.coord.col).First(),
            createAncientEventRoom: null,
            recorder: recorder,
            useAvailablePotions: false,
            createEventRoom: createEventRoom).RunAsync(maxFloors: 1);
    }

    private static (RunState RunState, Player Player) CreateAncientRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Ancient;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static async Task RunAncientAsync(
        bool useDriver,
        RunState runState,
        IRunRecorder recorder,
        Func<EventModel> createEvent)
    {
        Func<RunState, AbstractRoom> createAncientRoom = _ => new EventRoom(createEvent);
        if (useDriver)
        {
            await new RunDriver(
                runState,
                new FirstActionDecisionSource(),
                createAncientRoom,
                recorder).RunAsync(maxFloors: 0);
            return;
        }

        await new RunEngine(
            runState,
            points => points[0],
            createAncientRoom,
            recorder).RunAsync(maxFloors: 0);
    }

    private sealed class FirstActionDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            Task.FromResult(options[0]);

        public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
            Task.FromResult(options[0]);

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
        {
            Player player = state.Players.Single();
            CardModel? card = player.PlayerCombatState!.Hand.Cards.FirstOrDefault(candidate =>
                candidate.CanPlay(out _) &&
                (candidate.TargetType != TargetType.AnyEnemy || state.HittableEnemies.Count > 0));
            return Task.FromResult<CombatDecision>(card is null
                ? new CombatDecision.EndTurn()
                : new CombatDecision.PlayCard(
                    card,
                    card.TargetType == TargetType.AnyEnemy ? state.HittableEnemies[0] : null));
        }
    }
}
