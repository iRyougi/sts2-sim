using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Reporting;

file sealed class Task7CardLethalMonster : MonsterModel
{
    public override int MinInitialHp => 6;

    public override int MaxInitialHp => 6;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var wait = new MoveState("WAIT", _ => Task.CompletedTask, new DefendIntent());
        wait.FollowUpState = wait;
        return new MonsterMoveStateMachine([wait], wait);
    }
}

file sealed class Task7EnemyResolutionMonster : MonsterModel
{
    public override int MinInitialHp => 10;

    public override int MaxInitialHp => 10;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var expire = new MoveState(
            "EXPIRE",
            async _ =>
            {
                await CreatureCmd.LoseHp(
                    Creature.CombatState!.RunState,
                    Creature,
                    Creature.CurrentHp,
                    ValueProp.Unblockable | ValueProp.Unpowered);
            },
            new DefendIntent());
        expire.FollowUpState = expire;
        return new MonsterMoveStateMachine([expire], expire);
    }
}

file sealed class Task7FairyLethalMonster : MonsterModel
{
    public override int MinInitialHp => 100;

    public override int MaxInitialHp => 100;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var lethal = new MoveState(
            "LETHAL",
            _ => DamageCmd.Attack(999).FromMonster(this).Execute(),
            new SingleAttackIntent(999));
        lethal.FollowUpState = lethal;
        return new MonsterMoveStateMachine([lethal], lethal);
    }
}

file sealed class Task7ReportingAct : ActDefinition
{
    // 偏离 #311 的夹具需要第二幕；ValidateActs 要求 Act.Index 与其位置一致，故改为可配置。
    private readonly int _index;
    public override int Index => _index;
    public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();
    public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];
    private readonly Func<MonsterModel> _monsterFactory;
    private readonly IReadOnlyList<EncounterDefinition> _monsterEncounters;
    private readonly IReadOnlyList<EncounterDefinition> _otherEncounters;

    public Task7ReportingAct(Func<MonsterModel> monsterFactory, int index = 0)
    {
        _index = index;
        _monsterFactory = monsterFactory;
        _monsterEncounters =
        [
            new EncounterDefinition(CreateMonster, tags: null, isWeak: true, name: "task7-reporting-weak"),
            new EncounterDefinition(CreateMonster, tags: null, isWeak: false, name: "task7-reporting-regular"),
        ];
        _otherEncounters =
        [
            new EncounterDefinition(CreateMonster, tags: null, isWeak: false, name: "task7-reporting-other"),
        ];
    }

    public override int BaseNumberOfRooms => 15;

    public override int NumberOfWeakEncounters => 1;

    protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => _monsterEncounters;

    protected override IReadOnlyList<EncounterDefinition> EliteEncounters => _otherEncounters;

    protected override IReadOnlyList<EncounterDefinition> BossEncounters => _otherEncounters;

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);

    private MonsterModel CreateMonster() => _monsterFactory();
}

file sealed class Task7StrawberryRewardRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (!ReferenceEquals(player, Owner) || !roomType.IsCombatRoom())
        {
            return;
        }

        rewards.Add(new RelicReward(
            (RelicModel)ModelDb.Relic<Strawberry>().MutableClone(),
            player));
    }
}

file sealed class Task7ExtraPotionRewardRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool ShouldForcePotionReward(RoomType roomType) => roomType.IsCombatRoom();

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (!ReferenceEquals(player, Owner) || !roomType.IsCombatRoom())
        {
            return;
        }

        rewards.Add(new PotionReward(
            (PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone(),
            player));
    }
}

[Collection("ModelDb")]
public sealed class Task7ReportingIntegrationFixRound1Tests : IDisposable
{
    public Task7ReportingIntegrationFixRound1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(Task7CardLethalMonster))
                .Append(typeof(Task7EnemyResolutionMonster))
                .Append(typeof(Task7FairyLethalMonster))
                .Append(typeof(Task7StrawberryRewardRelic))
                .Append(typeof(Task7ExtraPotionRewardRelic)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CardLethalVictory_RecordsPostVictoryHpBeforeRealStrawberryReward(bool useDriver)
    {
        ReportingRun run = await CreateRunAsync(
            useDriver,
            "task7-card-lethal-strawberry-" + useDriver,
            () => (MonsterModel)ModelDb.Monster<Task7CardLethalMonster>().MutableClone(),
            configurePlayer: async player =>
            {
                ClearDeck(player);
                AddDeckCard<StrikeRegent>(player);
                AddDeckCard<StrikeRegent>(player);
                AddDeckCard<StrikeRegent>(player);
                AddDeckCard<StrikeRegent>(player);
                AddDeckCard<StrikeRegent>(player);
                await RelicCmd.Obtain(ModelDb.Relic<MeatOnTheBone>(), player);
                await RelicCmd.Obtain(ModelDb.Relic<Task7StrawberryRewardRelic>(), player);
                player.Creature.LoseHpInternal(45m, ValueProp.Unpowered);
            });

        TurnRecord finalTurn = Assert.Single(run.Log.Turns);
        Assert.Contains(finalTurn.Actions, action => action is PlayCardAction);
        Assert.Equal(42, finalTurn.PlayerPost.Hp);
        Assert.Equal(75, finalTurn.PlayerPost.MaxHp);
        Assert.Equal(49, run.Player.Creature.CurrentHp);
        Assert.Equal(82, run.Player.Creature.MaxHp);
        Assert.Contains(ModelDb.GetId<Strawberry>().ToString(), run.Log.Rewards.RelicsTaken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnemyResolutionVictory_RefreshesEndedTurnAfterVictoryAndBeforeBookReward(bool useDriver)
    {
        ReportingRun run = await CreateRunAsync(
            useDriver,
            "task7-enemy-resolution-book-" + useDriver,
            () => (MonsterModel)ModelDb.Monster<Task7EnemyResolutionMonster>().MutableClone(),
            configurePlayer: async player =>
            {
                ClearDeck(player);
                await RelicCmd.Obtain(ModelDb.Relic<BookOfFiveRings>(), player);
                await RelicCmd.Obtain(ModelDb.Relic<MeatOnTheBone>(), player);
                for (int i = 0; i < 4; i++)
                {
                    var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
                    dazed.AssignOwner(player);
                    await CardPileCmd.AddToDeck(dazed, ModelDb.Card<Dazed>());
                }

                player.Creature.LoseHpInternal(45m, ValueProp.Unpowered);
            });

        TurnRecord finalTurn = Assert.Single(run.Log.Turns);
        Assert.Contains(finalTurn.Actions.OfType<EnemyAction>(), action => action.MoveId == "EXPIRE");
        Assert.Equal(42, finalTurn.PlayerPost.Hp);
        Assert.Equal(62, run.Player.Creature.CurrentHp);
        Assert.Single(run.Log.Rewards.CardsTaken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BossLavaRock_RecordsBothResolvedRelicTakesWithoutCollapsing(bool useDriver)
    {
        ReportingRun run = await CreateRunAsync(
            useDriver,
            "task7-lava-rock-plural-" + useDriver,
            () => (MonsterModel)ModelDb.Monster<Task7CardLethalMonster>().MutableClone(),
            RoomType.Boss,
            async player => await RelicCmd.Obtain(ModelDb.Relic<LavaRock>(), player));

        Assert.Equal(2, run.Log.Rewards.RelicsTaken.Count);
        Assert.Null(run.Log.Rewards.RelicTaken);
        Assert.True(Assert.Single(run.Player.Relics.OfType<LavaRock>()).HasTriggered);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrayerWheelAndExtraPotion_KeepEveryCardAndPotionTake(bool useDriver)
    {
        ReportingRun run = await CreateRunAsync(
            useDriver,
            "task7-card-potion-plural-" + useDriver,
            () => (MonsterModel)ModelDb.Monster<Task7CardLethalMonster>().MutableClone(),
            configurePlayer: async player =>
            {
                await RelicCmd.Obtain(ModelDb.Relic<AmethystAubergine>(), player);
                await RelicCmd.Obtain(ModelDb.Relic<PrayerWheel>(), player);
                await RelicCmd.Obtain(ModelDb.Relic<Task7ExtraPotionRewardRelic>(), player);
            });

        Assert.Equal(2, run.Log.Rewards.CardsTaken.Count);
        Assert.Null(run.Log.Rewards.CardTaken);
        Assert.Equal(2, run.Log.Rewards.PotionsTaken.Count);
        Assert.Null(run.Log.Rewards.PotionTaken);
        Assert.Equal(run.Player.Gold - 99, run.Log.Rewards.Gold);
    }

    [Fact]
    public async Task RealFogmogIllusion_ReportsBothPowersAppliedToSummonedEye()
    {
        ReportingRun run = await CreateRunAsync(
            useDriver: false,
            "task7-real-fogmog-powers",
            () => (MonsterModel)ModelDb.Monster<Fogmog>().MutableClone(),
            configurePlayer: player =>
            {
                ClearDeck(player);
                for (int i = 0; i < 5; i++)
                {
                    AddDeckCard<Dazed>(player);
                }

                return Task.CompletedTask;
            });

        EnemyAction illusion = Assert.Single(
            run.Log.Turns
                .SelectMany(turn => turn.Actions)
                .OfType<EnemyAction>(),
            action => action.MoveId == "ILLUSION_MOVE");
        Assert.Contains(
            illusion.PowersApplied,
            application => application.Target == "illusion" &&
                           application.Power == ModelDb.GetId<IllusionPower>().ToString() &&
                           application.Amount == 1);
        Assert.Contains(
            illusion.PowersApplied,
            application => application.Target == "illusion" &&
                           application.Power == ModelDb.GetId<MinionPower>().ToString() &&
                           application.Amount == 1);

        string markdown = MarkdownReportRenderer.Render(run.Manifest, run.CombatLogs);
        Assert.Contains("ILLUSION_MOVE", markdown, StringComparison.Ordinal);
        Assert.Contains("POWER.ILLUSION_POWER+1", markdown, StringComparison.Ordinal);
        Assert.Contains("POWER.MINION_POWER+1", markdown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ManualAmbergris_ReportsHealingConsumptionAndInventoryRemoval(bool useDriver)
    {
        ReportingRun run = await CreateRunAsync(
            useDriver,
            "task7-manual-healing-potion-" + useDriver,
            () => (MonsterModel)ModelDb.Monster<Task7CardLethalMonster>().MutableClone(),
            configurePlayer: player =>
            {
                player.Creature.LoseHpInternal(45m, ValueProp.Unpowered);
                player.AddPotionInternal((PotionModel)ModelDb.Potion<Ambergris>().MutableClone());
                return Task.CompletedTask;
            },
            useAvailablePotions: true);

        UsePotionAction action = Assert.Single(run.Log.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<UsePotionAction>());
        Assert.Equal(ModelDb.GetId<Ambergris>().ToString(), action.Potion);
        Assert.Equal(37, action.HealingReceived);
        Assert.True(action.Consumed);
        Assert.Empty(action.PotionsAdded);
        Assert.Equal([ModelDb.GetId<Ambergris>().ToString()], action.PotionsRemoved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticFairy_ReportsHealingConsumptionAndInventoryRemoval(bool useDriver)
    {
        ReportingRun run = await CreateRunAsync(
            useDriver,
            "task7-automatic-fairy-" + useDriver,
            () => (MonsterModel)ModelDb.Monster<Task7FairyLethalMonster>().MutableClone(),
            configurePlayer: player =>
            {
                ClearDeck(player);
                for (int i = 0; i < 5; i++)
                {
                    AddDeckCard<Dazed>(player);
                }

                player.AddPotionInternal((PotionModel)ModelDb.Potion<FairyInABottle>().MutableClone());
                return Task.CompletedTask;
            });

        UsePotionAction action = Assert.Single(run.Log.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<UsePotionAction>());
        Assert.Equal(ModelDb.GetId<FairyInABottle>().ToString(), action.Potion);
        Assert.Equal(22, action.HealingReceived);
        Assert.True(action.Consumed);
        Assert.Empty(action.PotionsAdded);
        Assert.Equal([ModelDb.GetId<FairyInABottle>().ToString()], action.PotionsRemoved);
        Assert.DoesNotContain(
            run.Player.PotionSlots,
            potion => potion is FairyInABottle);
    }

    private static async Task<ReportingRun> CreateRunAsync(
        bool useDriver,
        string seed,
        Func<MonsterModel> monsterFactory,
        RoomType roomType = RoomType.Monster,
        Func<Player, Task>? configurePlayer = null,
        bool useAvailablePotions = false)
    {
        // 偏离 #311：最终幕 Boss 不发奖励，故 Boss 场景须为多幕，让这场 Boss 不是最终幕。
        // 非 Boss 场景保持单幕——本夹具为全文件共用，改幕数会连带改变地图生成与 RNG 序。
        var runState = roomType == RoomType.Boss
            ? new RunState(seed, [new Task7ReportingAct(monsterFactory), new Task7ReportingAct(monsterFactory, index: 1)])
            : new RunState(seed, new Task7ReportingAct(monsterFactory));
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        MapPoint point = PickFirstByCoord(runState.Map.StartingMapPoint.Children);
        point.PointType = roomType switch
        {
            RoomType.Monster => MapPointType.Monster,
            RoomType.Elite => MapPointType.Elite,
            RoomType.Boss => MapPointType.Boss,
            _ => throw new ArgumentOutOfRangeException(nameof(roomType), roomType, null),
        };

        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        if (configurePlayer is not null)
        {
            await configurePlayer(player);
        }

        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);
        if (useDriver)
        {
            var driver = new RunDriver(
                runState,
                new Task7DecisionSource(),
                createAncientEventRoom: null,
                recorder: recorder,
                useAvailablePotions: useAvailablePotions);
            await driver.RunAsync(maxFloors: 1);
        }
        else
        {
            var engine = new RunEngine(
                runState,
                PickFirstByCoord,
                createAncientEventRoom: null,
                recorder: recorder,
                useAvailablePotions: useAvailablePotions);
            await engine.RunAsync(maxFloors: 1);
        }

        return new ReportingRun(
            player,
            recorder.BuildManifest(),
            recorder.CombatLogs,
            Assert.Single(recorder.CombatLogs).Value);
    }

    private static void ClearDeck(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }
    }

    private static TCard AddDeckCard<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Deck);
        return card;
    }

    private static MapPoint PickFirstByCoord(IEnumerable<MapPoint> points) =>
        points.OrderBy(point => point.coord.col).First();

    private sealed record ReportingRun(
        Player Player,
        RunManifest Manifest,
        IReadOnlyDictionary<string, CombatLog> CombatLogs,
        CombatLog Log);

    private sealed class Task7DecisionSource : IRunDecisionSource
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
}
