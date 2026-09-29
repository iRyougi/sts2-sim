using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Reporting;

file sealed class CombatReportingProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public int BeforeCombatStartCount { get; private set; }

    public int VictoryCount { get; private set; }

    public int CombatEndCount { get; private set; }

    public int BeforePotionCount { get; private set; }

    public int AfterPotionCount { get; private set; }

    public override Task BeforeCombatStart()
    {
        BeforeCombatStartCount++;
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory()
    {
        VictoryCount++;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        CombatEndCount++;
        return Task.CompletedTask;
    }

    public override Task BeforePotionUsed(PotionModel potion, Player player)
    {
        BeforePotionCount++;
        return Task.CompletedTask;
    }

    public override Task AfterPotionUsed(PotionModel potion, Player player)
    {
        AfterPotionCount++;
        return Task.CompletedTask;
    }
}

file sealed class CombatReportingMayhemRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task BeforeCombatStart() => PowerCmd.Apply<MayhemPower>(
        Owner.Creature.CombatState!,
        Owner.Creature,
        1m,
        Owner.Creature,
        cardSource: null);
}

file sealed class CombatReportingStartTurnLethalRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        return side == CombatSide.Player && participants.Contains(Owner.Creature)
            ? CreatureCmd.LoseHp(
                Owner.RunState,
                Owner.Creature,
                Owner.Creature.CurrentHp,
                ValueProp.Unblockable)
            : Task.CompletedTask;
    }
}

file sealed class CombatReportingSetupRelic : RelicModel
{
    private bool _ranAfterPlayerTurnStart;

    public override RelicRarity Rarity => RelicRarity.Common;

    public Action<Player>? ConfigureCombat { get; set; }

    public Func<Player, Task>? OnPlayerTurnStart { get; set; }

    public override Task BeforeCombatStart()
    {
        ConfigureCombat?.Invoke(Owner);
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (_ranAfterPlayerTurnStart ||
            side != CombatSide.Player ||
            !participants.Contains(Owner.Creature) ||
            OnPlayerTurnStart is null)
        {
            return Task.CompletedTask;
        }

        _ranAfterPlayerTurnStart = true;
        return OnPlayerTurnStart(Owner);
    }
}

file sealed class CombatReportingNestedSkillCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay) => DamageCmd
        .Attack(6m)
        .FromCard(this, cardPlay)
        .TargetingAllOpponents(CombatState!)
        .Execute();
}

file sealed class CombatReportingMonster : MonsterModel
{
    private bool _summoned;

    public override int MinInitialHp => 60;

    public override int MaxInitialHp => 60;

    public int PerformedMoveCount { get; private set; }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState(
            "REPORTING_MOVE",
            PerformReportingMove,
            new SingleAttackIntent(4),
            new DefendIntent(),
            new BuffIntent(),
            new SummonIntent());
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }

    private async Task PerformReportingMove(IReadOnlyList<Creature> targets)
    {
        PerformedMoveCount++;
        await DamageCmd.Attack(4).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(
            Creature.CombatState!,
            Creature,
            5m,
            ValueProp.Move,
            cardSource: null,
            cardPlay: null);
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);

        if (!_summoned)
        {
            _summoned = true;
            await CreatureCmd.Add(
                (EyeWithTeeth)ModelDb.Monster<EyeWithTeeth>().MutableClone(),
                Creature.CombatState!,
                Creature.Side,
                "reporting_summon");
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _summoned = false;
        PerformedMoveCount = 0;
    }
}

file sealed class CombatReportingLethalMonster : MonsterModel
{
    public override int MinInitialHp => 100;

    public override int MaxInitialHp => 100;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState(
            "LETHAL_MOVE",
            _ => DamageCmd.Attack(999).FromMonster(this).Execute(),
            new SingleAttackIntent(999));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}

file sealed class CombatReportingEdgeMonster : MonsterModel
{
    public override int MinInitialHp => 36;

    public override int MaxInitialHp => 36;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("WAIT", _ => Task.CompletedTask, new DefendIntent());
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}

file sealed class CombatReportingOwnershipMonster : MonsterModel
{
    public override int MinInitialHp => 18;

    public override int MaxInitialHp => 18;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState(
            "OWNERSHIP_FAIL_FAST",
            _ => DamageCmd.Attack(999).FromMonster(this).Execute(),
            new SingleAttackIntent(999));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}

file sealed class CombatReportingAct : ActDefinition
{
    public override int Index => 0;
    public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();
    public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];
    private readonly Func<MonsterModel> _monsterFactory;
    private readonly IReadOnlyList<EncounterDefinition> _monsterEncounters;
    private readonly IReadOnlyList<EncounterDefinition> _otherEncounters;

    public CombatReportingAct(Func<MonsterModel> monsterFactory)
    {
        _monsterFactory = monsterFactory;
        _monsterEncounters =
        [
            new EncounterDefinition(CreateMonster, tags: null, isWeak: true, name: "CombatReportingIntro"),
            new EncounterDefinition(CreateMonster, tags: null, isWeak: false, name: "CombatReportingNormal"),
        ];
        _otherEncounters = [new EncounterDefinition(CreateMonster, tags: null, isWeak: false, name: "CombatReportingOther")];
    }

    public MonsterModel? CreatedMonster { get; private set; }

    public override int BaseNumberOfRooms => 15;

    public override int NumberOfWeakEncounters => 1;

    protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => _monsterEncounters;

    protected override IReadOnlyList<EncounterDefinition> EliteEncounters => _otherEncounters;

    protected override IReadOnlyList<EncounterDefinition> BossEncounters => _otherEncounters;

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);

    private MonsterModel CreateMonster()
    {
        CreatedMonster = _monsterFactory();
        return CreatedMonster;
    }
}

[Collection("ModelDb")]
public sealed class CombatDetailRecordingTests : IDisposable
{
    public CombatDetailRecordingTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(CombatReportingProbeRelic))
                .Append(typeof(CombatReportingMayhemRelic))
                .Append(typeof(CombatReportingStartTurnLethalRelic))
                .Append(typeof(CombatReportingSetupRelic))
                .Append(typeof(CombatReportingNestedSkillCard))
                .Append(typeof(CombatReportingMonster))
                .Append(typeof(CombatReportingEdgeMonster))
                .Append(typeof(CombatReportingOwnershipMonster))
                .Append(typeof(CombatReportingLethalMonster)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcreteRecorder_RecordsRealCombatActionsSummonFinalStateAndRewards(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch));

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        Assert.Equal("Monster", log.EncounterType);
        Assert.Equal("CombatReportingIntro", log.EncounterName);
        Assert.Equal("victory", log.Result);
        Assert.NotEmpty(log.Turns);
        Assert.All(log.Turns, turn => Assert.Equal("player", turn.Side));
        Assert.NotEmpty(log.Turns.SelectMany(turn => turn.Draws));
        Assert.NotEmpty(log.Turns.SelectMany(turn => turn.Actions).OfType<PlayCardAction>());

        TurnRecord summonTurn = Assert.Single(log.Turns, turn =>
            turn.Actions.OfType<EnemyAction>().Any(action => action.MoveId == "REPORTING_MOVE") &&
            turn.EnemiesPost.Count > turn.EnemiesPre.Count);
        EnemyAction enemyAction = Assert.Single(summonTurn.Actions.OfType<EnemyAction>());
        Assert.Equal("REPORTING_MOVE", enemyAction.MoveId);
        Assert.NotEmpty(enemyAction.DamageToTargets);
        Assert.Contains(enemyAction.PowersApplied, power => power.Power == ModelDb.GetId<StrengthPower>().ToString());
        Assert.Contains(summonTurn.EnemiesPost, enemy => enemy.Slot == "reporting_summon");
        Assert.True(summonTurn.EnemiesPost.Single(enemy => enemy.Id == ModelDb.GetId<CombatReportingMonster>().ToString()).Block > 0);
        Assert.IsType<EndTurnAction>(summonTurn.Actions[^1]);

        Assert.Equal(capture.Result.FinalPlayerHp, log.Turns[^1].PlayerPost.Hp);
        Assert.All(log.Turns[^1].EnemiesPost, enemy => Assert.Equal(0, enemy.Hp));
        Assert.True(log.Rewards.Gold > 0);
        Assert.Equal(3, log.Rewards.CardsOffered.Count);
        Assert.NotNull(log.Rewards.CardTaken);
        Assert.True(capture.PlayerGold >= log.Rewards.Gold);
        Assert.Contains(capture.Deck, card => card == log.Rewards.CardTaken);
        Assert.Equal(capture.Result.Won, capture.Manifest.Won());
        Assert.True(Assert.IsType<CombatFloorDetail>(Assert.Single(capture.Manifest.Floors).Detail).Victory);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullPotionBelt_RecordsResolvedNoOpRewardAsNotTaken(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            forcePotionRewardWithFullBelt: true);

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        Assert.Null(log.Rewards.PotionTaken);
        Assert.DoesNotContain(capture.Potions, potion => potion is null);
        Assert.All(
            capture.Potions,
            potion => Assert.Equal(ModelDb.GetId<StrengthPotion>().ToString(), potion));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealExtraRewards_ProjectAllOptionsAndAvoidFabricatingSingularSelections(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            addAmethystAubergine: true,
            addPrayerWheel: true);

        CombatRewards rewards = Assert.Single(capture.CombatLogs).Value.Rewards;
        Assert.Equal(capture.PlayerGold - 99, rewards.Gold);
        Assert.Equal(6, rewards.CardsOffered.Count);
        Assert.Null(rewards.CardTaken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RecorderPresence_IsObservationalAcrossDecisionsRngStateHooksRewardsAndCallbacks(bool useDriver)
    {
        RunCapture withoutRecorder = await RunOnceAsync(useDriver, recorder: null);
        RunCapture withRecorder = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch));

        Assert.Equal(withoutRecorder.Result, withRecorder.Result);
        Assert.Equal(withoutRecorder.Decisions, withRecorder.Decisions);
        Assert.Equal(withoutRecorder.RunRngCounters, withRecorder.RunRngCounters);
        Assert.Equal(withoutRecorder.PlayerRngCounters, withRecorder.PlayerRngCounters);
        Assert.Equal(withoutRecorder.PlayerHp, withRecorder.PlayerHp);
        Assert.Equal(withoutRecorder.PlayerGold, withRecorder.PlayerGold);
        Assert.Equal(withoutRecorder.Deck, withRecorder.Deck);
        Assert.Equal(withoutRecorder.Relics, withRecorder.Relics);
        Assert.Equal(withoutRecorder.Potions, withRecorder.Potions);
        Assert.Equal(withoutRecorder.RoomCallbacks, withRecorder.RoomCallbacks);
        Assert.Equal(withoutRecorder.HookCounts, withRecorder.HookCounts);
        Assert.Equal(withoutRecorder.MonsterMoveCount, withRecorder.MonsterMoveCount);
        Assert.Equal(withoutRecorder.PlayerCombatStateCleared, withRecorder.PlayerCombatStateCleared);
        Assert.Equal(withoutRecorder.CreatureCombatStateCleared, withRecorder.CreatureCombatStateCleared);

        CombatLog recorded = Assert.Single(withRecorder.CombatLogs).Value;
        var detail = Assert.IsType<CombatFloorDetail>(Assert.Single(withRecorder.Manifest.Floors).Detail);
        Assert.Equal(detail.Victory ? "victory" : "defeat", recorded.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PotionPolicy_IsSeparateDefaultFalseOptInAndUsesNormalLifecycle(bool useDriver)
    {
        RunCapture recorderOnly = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            addStrengthPotion: true);
        Assert.Contains(recorderOnly.Potions, potion => potion == ModelDb.GetId<StrengthPotion>().ToString());
        Assert.Empty(Assert.Single(recorderOnly.CombatLogs).Value.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<UsePotionAction>());
        Assert.Equal((1, 1, 1, 0, 0), recorderOnly.HookCounts);

        RunCapture policyWithoutRecorder = await RunOnceAsync(
            useDriver,
            recorder: null,
            addStrengthPotion: true,
            useAvailablePotions: true);
        Assert.DoesNotContain(policyWithoutRecorder.Potions, potion => potion == ModelDb.GetId<StrengthPotion>().ToString());
        Assert.Equal((1, 1, 1, 1, 1), policyWithoutRecorder.HookCounts);

        RunCapture policyWithRecorder = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            addStrengthPotion: true,
            useAvailablePotions: true);
        UsePotionAction action = Assert.Single(Assert.Single(policyWithRecorder.CombatLogs).Value.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<UsePotionAction>());
        Assert.Equal(ModelDb.GetId<StrengthPotion>().ToString(), action.Potion);
        Assert.Equal("player", action.Target);
        Assert.Contains(action.PowersApplied, power => power.Power == ModelDb.GetId<StrengthPower>().ToString());
        Assert.Equal((1, 1, 1, 1, 1), policyWithRecorder.HookCounts);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutoplayPotion_IsOneSuccessfulPotionActionWithoutDuplicateCardActions(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            useAvailablePotions: true,
            configureDeck: ConfigureAutoplayDeck,
            addPotion: ModelDb.Potion<DistilledChaos>(),
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingEdgeMonster>().MutableClone());

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        ActionRecord[] actions = log.Turns.SelectMany(turn => turn.Actions).ToArray();
        UsePotionAction potion = Assert.Single(actions.OfType<UsePotionAction>());
        Assert.Equal(ModelDb.GetId<DistilledChaos>().ToString(), potion.Potion);
        Assert.Equal("player", potion.Target);
        Assert.Equal(18, Assert.Single(potion.DamageDealt).Amount);
        Assert.IsType<UsePotionAction>(actions[0]);
        Assert.Equal(3, actions.OfType<PlayCardAction>().Count());
        Assert.All(actions.OfType<PlayCardAction>(), action => Assert.Equal(1, action.PlayCount));
        Assert.Equal("victory", log.Result);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task DriveLoop_DoesNotActOrConsumePotionAfterStartTurnCombatEnds(
        bool useDriver,
        bool useAvailablePotions)
    {
        var decisionSource = new EndTurnDecisionSource();
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            decisionSource: useDriver ? decisionSource : null,
            addStrengthPotion: true,
            useAvailablePotions: useAvailablePotions,
            addStartTurnLethalRelic: true);

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        Assert.Equal("defeat", log.Result);
        Assert.Contains(capture.Potions, potion => potion == ModelDb.GetId<StrengthPotion>().ToString());
        Assert.Empty(log.Turns.SelectMany(turn => turn.Actions));
        Assert.DoesNotContain("end-turn", decisionSource.Calls);
        Assert.Equal(0, capture.HookCounts.BeforePotion);
        Assert.Equal(0, capture.HookCounts.AfterPotion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothDrivers_RecordStartTurnMayhemAutoplayOnceOutsidePotion(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: ConfigureMayhemDeck,
            configureCombat: ConfigureMayhemCombat,
            decisionSource: useDriver ? new EndTurnDecisionSource() : null,
            addMayhemRelic: true,
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingLethalMonster>().MutableClone());

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        PlayCardAction autoplay = Assert.Single(log.Turns[0].Actions.OfType<PlayCardAction>());
        Assert.Equal(ModelDb.GetId<StrikeRegent>().ToString(), autoplay.Card);
        Assert.Equal(0, autoplay.EnergyCost);
        Assert.Equal(1, autoplay.PlayCount);
        Assert.IsType<PlayCardAction>(log.Turns[0].Actions[0]);
        Assert.Equal("defeat", log.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticPotion_UsesNormalLifecycleAndRecordsOnlyAfterSuccessfulUse(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: ConfigureDazedOpeningDeck,
            decisionSource: useDriver ? new EndTurnDecisionSource() : null,
            addPotion: ModelDb.Potion<FairyInABottle>(),
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingLethalMonster>().MutableClone());

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        TurnRecord fairyTurn = Assert.Single(log.Turns, turn =>
            turn.Actions.Any(action => action is UsePotionAction));
        ActionRecord[] actions = fairyTurn.Actions.ToArray();
        EnemyAction enemy = Assert.IsType<EnemyAction>(actions[0]);
        UsePotionAction potion = Assert.IsType<UsePotionAction>(actions[1]);
        DamageTakenRecord damage = Assert.Single(enemy.DamageToTargets);
        Assert.Equal(75, damage.Amount);
        Assert.Equal(0, damage.BlockAbsorbed);
        Assert.Equal(22, fairyTurn.PlayerPost.Hp);
        Assert.Equal(ModelDb.GetId<FairyInABottle>().ToString(), potion.Potion);
        Assert.Equal("player", potion.Target);
        Assert.DoesNotContain(capture.Potions, id => id == ModelDb.GetId<FairyInABottle>().ToString());
        Assert.Equal(1, capture.HookCounts.BeforePotion);
        Assert.Equal(1, capture.HookCounts.AfterPotion);
        Assert.Equal("defeat", log.Result);
    }

    [Fact]
    public async Task RoyalPoisonLethalStart_RecordsAutomaticFairyInPreparedTurn()
    {
        RunCapture capture = await RunOnceAsync(
            useDriver: true,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: player =>
            {
                ConfigureDazedOpeningDeck(player);
                player.Creature.SetCurrentHpInternal(4m);
                var royalPoison = (RelicModel)ModelDb
                    .Relic<Sts2Sim.Core.Models.Relics.RoyalPoison>().MutableClone();
                royalPoison.AssignOwner(player);
                player.AddRelicInternal(royalPoison);
            },
            decisionSource: new EndTurnDecisionSource(),
            addPotion: ModelDb.Potion<FairyInABottle>(),
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingLethalMonster>().MutableClone());

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        TurnRecord firstTurn = log.Turns[0];
        UsePotionAction fairy = Assert.Single(firstTurn.Actions.OfType<UsePotionAction>());
        Assert.IsType<UsePotionAction>(firstTurn.Actions[0]);
        Assert.Equal(4, firstTurn.PlayerPre.Hp);
        Assert.Equal(ModelDb.GetId<FairyInABottle>().ToString(), fairy.Potion);
        Assert.Equal(22, fairy.HealingReceived);
        Assert.True(fairy.Consumed);
        Assert.DoesNotContain(capture.Potions, id => id == fairy.Potion);
        Assert.Equal("defeat", log.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TopLevelHookPlay_IsObservedExactlyOnceByCommonCardBoundary(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: ConfigureHookPlayDeck,
            configureCombat: ConfigureHookPlayCombat,
            afterPlayerTurnStart: async player =>
            {
                StrikeRegent strike = Assert.Single(player.PlayerCombatState!.DrawPile.Cards.OfType<StrikeRegent>());
                await strike.PlayAsync(player.Creature.CombatState!.HittableEnemies.Single());
            },
            decisionSource: useDriver ? new EndTurnDecisionSource() : null,
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingLethalMonster>().MutableClone());

        PlayCardAction play = Assert.Single(Assert.Single(capture.CombatLogs).Value.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<PlayCardAction>());
        Assert.Equal(ModelDb.GetId<StrikeRegent>().ToString(), play.Card);
        Assert.Equal(1, play.EnergyCost);
        Assert.Equal(6, Assert.Single(play.DamageDealt).Amount);
        Assert.Equal(1, play.PlayCount);
    }

    [Theory]
    [InlineData(false, nameof(BeatDown))]
    [InlineData(true, nameof(BeatDown))]
    [InlineData(false, nameof(Catastrophe))]
    [InlineData(true, nameof(Catastrophe))]
    [InlineData(false, nameof(DecisionsDecisions))]
    [InlineData(true, nameof(DecisionsDecisions))]
    [InlineData(false, nameof(IAmInvincible))]
    [InlineData(true, nameof(IAmInvincible))]
    public async Task NestedNamedCardPlays_AreAbsorbedIntoSingleParentAction(
        bool useDriver,
        string scenario)
    {
        Type parentType = NestedParentType(scenario);
        bool invincible = scenario == nameof(IAmInvincible);
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: player => ConfigureNestedOwnershipDeck(player, scenario),
            configureCombat: player => ConfigureNestedOwnershipCombat(player, scenario),
            decisionSource: useDriver ? new CardTypeDecisionSource(parentType) : null,
            monsterFactory: invincible
                ? () => (MonsterModel)ModelDb.Monster<CombatReportingLethalMonster>().MutableClone()
                : () => (MonsterModel)ModelDb.Monster<CombatReportingOwnershipMonster>().MutableClone());

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        PlayCardAction play = Assert.Single(log.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<PlayCardAction>());
        Assert.Equal(ModelDb.GetId(parentType).ToString(), play.Card);
        Assert.Equal(1, play.PlayCount);
        Assert.Equal(
            scenario switch
            {
                nameof(BeatDown) => 3,
                nameof(Catastrophe) => 2,
                nameof(IAmInvincible) => 1,
                _ => 0,
            },
            play.EnergyCost);

        if (invincible)
        {
            Assert.Empty(play.DamageDealt);
            Assert.Equal(15, play.BlockGained);
            Assert.Equal("defeat", log.Result);
        }
        else
        {
            int expectedDamage = scenario == nameof(Catastrophe) ? 12 : 18;
            Assert.Equal(expectedDamage, Assert.Single(play.DamageDealt).Amount);
            Assert.Equal(
                scenario == nameof(Catastrophe) ? "defeat" : "victory",
                log.Result);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PotionOwnedAutoplays_AreAbsorbedIntoSinglePotionAction(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            useAvailablePotions: true,
            configureDeck: ConfigureDistilledChaosOwnershipDeck,
            configureCombat: ConfigureDistilledChaosOwnershipCombat,
            decisionSource: useDriver ? new EndTurnDecisionSource() : null,
            addPotion: ModelDb.Potion<DistilledChaos>(),
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingOwnershipMonster>().MutableClone());

        ActionRecord action = Assert.Single(Assert.Single(capture.CombatLogs).Value.Turns
            .SelectMany(turn => turn.Actions));
        UsePotionAction potion = Assert.IsType<UsePotionAction>(action);
        Assert.Equal(ModelDb.GetId<DistilledChaos>().ToString(), potion.Potion);
        Assert.Equal(18, Assert.Single(potion.DamageDealt).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplayedFreeCard_IsOneActionWithResolvedPlayCountAndNoDuplicateCharge(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: ConfigureReplayOwnershipDeck,
            configureCombat: ConfigureReplayOwnershipCombat,
            decisionSource: useDriver ? new CardTypeDecisionSource(typeof(StrikeRegent)) : null,
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingOwnershipMonster>().MutableClone());

        PlayCardAction play = Assert.Single(Assert.Single(capture.CombatLogs).Value.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<PlayCardAction>());
        Assert.Equal(ModelDb.GetId<StrikeRegent>().ToString(), play.Card);
        Assert.Equal(0, play.EnergyCost);
        Assert.Equal(3, play.PlayCount);
        Assert.Equal(18, Assert.Single(play.DamageDealt).Amount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TopLevelXCard_RecordsGrossResolvedSpendOnce(bool useDriver)
    {
        RunCapture capture = await RunOnceAsync(
            useDriver,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: ConfigureXOwnershipDeck,
            configureCombat: ConfigureXOwnershipCombat,
            decisionSource: useDriver ? new CardTypeDecisionSource(typeof(Volley)) : null,
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingOwnershipMonster>().MutableClone());

        PlayCardAction play = Assert.Single(Assert.Single(capture.CombatLogs).Value.Turns
            .SelectMany(turn => turn.Actions)
            .OfType<PlayCardAction>());
        Assert.Equal(ModelDb.GetId<Volley>().ToString(), play.Card);
        Assert.Equal(3, play.EnergyCost);
        Assert.Equal(1, play.PlayCount);
        Assert.Equal(18, Assert.Single(play.DamageDealt).Amount);
    }

    [Fact]
    public async Task RunDriver_RecordsResolvedXFreeReplayOnceAndOmitsEndTurnForLethalCard()
    {
        RunCapture capture = await RunOnceAsync(
            useDriver: true,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            configureDeck: ConfigureResolvedCardDeck,
            decisionSource: new ResolvedCardDecisionSource(),
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingEdgeMonster>().MutableClone());

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        PlayCardAction[] plays = log.Turns.SelectMany(turn => turn.Actions).OfType<PlayCardAction>().ToArray();
        PlayCardAction volley = Assert.Single(plays, play => play.Card == ModelDb.GetId<Volley>().ToString());
        PlayCardAction replayedFreeStrike = Assert.Single(plays, play => play.Card == ModelDb.GetId<StrikeRegent>().ToString());
        Assert.Equal(3, volley.EnergyCost);
        Assert.Equal(0, replayedFreeStrike.EnergyCost);
        Assert.Equal(3, replayedFreeStrike.PlayCount);
        Assert.DoesNotContain(log.Turns[^1].Actions, action => action is EndTurnAction);
        Assert.Equal("victory", log.Result);
    }

    [Fact]
    public async Task RunDriver_DrawOnlyDefeatRecordsEnemyMoveAndExplicitEndTurnWithEmptyRewards()
    {
        RunCapture capture = await RunOnceAsync(
            useDriver: true,
            recorder: new RunRecorder(() => DateTimeOffset.UnixEpoch),
            decisionSource: new EndTurnDecisionSource(),
            monsterFactory: () => (MonsterModel)ModelDb.Monster<CombatReportingLethalMonster>().MutableClone());

        CombatLog log = Assert.Single(capture.CombatLogs).Value;
        TurnRecord turn = Assert.Single(log.Turns);
        Assert.NotEmpty(turn.Draws);
        Assert.Empty(turn.Actions.OfType<PlayCardAction>());
        Assert.Equal("LETHAL_MOVE", Assert.Single(turn.Actions.OfType<EnemyAction>()).MoveId);
        Assert.IsType<EndTurnAction>(turn.Actions[^1]);
        Assert.Equal("defeat", log.Result);
        Assert.Equal(0, log.Rewards.Gold);
        Assert.Empty(log.Rewards.CardsOffered);
        Assert.Null(log.Rewards.CardTaken);
        Assert.Equal(0, turn.PlayerPost.Hp);
    }

    private static async Task<RunCapture> RunOnceAsync(
        bool useDriver,
        RunRecorder? recorder,
        bool addStrengthPotion = false,
        bool useAvailablePotions = false,
        Action<Player>? configureDeck = null,
        RecordingDecisionSource? decisionSource = null,
        PotionModel? addPotion = null,
        bool addMayhemRelic = false,
        bool addStartTurnLethalRelic = false,
        bool forcePotionRewardWithFullBelt = false,
        Action<Player>? configureCombat = null,
        Func<Player, Task>? afterPlayerTurnStart = null,
        Func<MonsterModel>? monsterFactory = null,
        bool addAmethystAubergine = false,
        bool addPrayerWheel = false)
    {
        var act = new CombatReportingAct(monsterFactory ?? (() =>
            (MonsterModel)ModelDb.Monster<CombatReportingMonster>().MutableClone()));
        var runState = new RunState("task-7-combat-reporting", act);
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        MapPoint firstPoint = runState.Map.StartingMapPoint.Children.OrderBy(point => point.coord.col).First();
        firstPoint.PointType = MapPointType.Monster;

        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        var probe = (CombatReportingProbeRelic)ModelDb.Relic<CombatReportingProbeRelic>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        if (addAmethystAubergine)
        {
            var aubergine = (RelicModel)ModelDb
                .Relic<Sts2Sim.Core.Models.Relics.AmethystAubergine>()
                .MutableClone();
            aubergine.AssignOwner(player);
            player.AddRelicInternal(aubergine);
        }

        if (addPrayerWheel)
        {
            var prayerWheel = (RelicModel)ModelDb
                .Relic<Sts2Sim.Core.Models.Relics.PrayerWheel>()
                .MutableClone();
            prayerWheel.AssignOwner(player);
            player.AddRelicInternal(prayerWheel);
        }

        if (addMayhemRelic)
        {
            var mayhemRelic = (CombatReportingMayhemRelic)ModelDb.Relic<CombatReportingMayhemRelic>().MutableClone();
            mayhemRelic.AssignOwner(player);
            player.AddRelicInternal(mayhemRelic);
        }
        if (addStartTurnLethalRelic)
        {
            var lethalRelic = (CombatReportingStartTurnLethalRelic)ModelDb.Relic<CombatReportingStartTurnLethalRelic>().MutableClone();
            lethalRelic.AssignOwner(player);
            player.AddRelicInternal(lethalRelic);
        }
        if (configureCombat is not null || afterPlayerTurnStart is not null)
        {
            var setupRelic = (CombatReportingSetupRelic)ModelDb.Relic<CombatReportingSetupRelic>().MutableClone();
            setupRelic.ConfigureCombat = configureCombat;
            setupRelic.OnPlayerTurnStart = afterPlayerTurnStart;
            setupRelic.AssignOwner(player);
            player.AddRelicInternal(setupRelic);
        }
        if (forcePotionRewardWithFullBelt)
        {
            var whiteBeast = (RelicModel)ModelDb.Relic<Sts2Sim.Core.Models.Relics.WhiteBeastStatue>().MutableClone();
            whiteBeast.AssignOwner(player);
            player.AddRelicInternal(whiteBeast);
            while (player.PotionSlots.Contains(null))
            {
                var potion = (PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone();
                player.AddPotionInternal(potion);
            }
        }
        if (addStrengthPotion)
        {
            player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        }
        if (addPotion is not null)
        {
            player.AddPotionInternal(addPotion);
        }
        configureDeck?.Invoke(player);
        runState.AddPlayer(player);

        var decisions = new List<string>();
        var callbacks = new List<string>();
        RunEngine.Result? engineResult = null;
        RunDriver.Result? driverResult = null;
        if (useDriver)
        {
            decisionSource ??= new RecordingDecisionSource();
            var driver = new RunDriver(
                runState,
                decisionSource,
                createAncientEventRoom: null,
                recorder: recorder,
                useAvailablePotions: useAvailablePotions);
            driver.OnRoomResolved += (point, roomType) => callbacks.Add($"{point.coord}:{roomType}");
            driverResult = await driver.RunAsync(maxFloors: 1);
            decisions.AddRange(decisionSource.Calls);
        }
        else
        {
            MapPoint ChoosePoint(IReadOnlyList<MapPoint> options)
            {
                MapPoint selected = options.OrderBy(point => point.coord.col).First();
                decisions.Add($"map:{selected.coord}");
                return selected;
            }

            var engine = new RunEngine(
                runState,
                ChoosePoint,
                createAncientEventRoom: null,
                recorder: recorder,
                useAvailablePotions: useAvailablePotions);
            engine.OnRoomResolved += (point, roomType) => callbacks.Add($"{point.coord}:{roomType}");
            engineResult = await engine.RunAsync(maxFloors: 1);
        }

        (bool Won, bool ReachedBoss, int FloorsVisited, int FinalPlayerHp) result = useDriver
            ? (driverResult!.Won, driverResult.ReachedBoss, driverResult.FloorsVisited, driverResult.FinalPlayerHp)
            : (engineResult!.Won, engineResult.ReachedBoss, engineResult.FloorsVisited, engineResult.FinalPlayerHp);
        IReadOnlyDictionary<string, CombatLog> combatLogs = recorder?.CombatLogs
            ?? new Dictionary<string, CombatLog>();
        RunManifest? manifest = recorder?.BuildManifest();
        return new RunCapture(
            result,
            decisions.ToArray(),
            Enum.GetValues<RunRngType>().Select(type => runState.Rng.GetRng(type).Counter).ToArray(),
            Enum.GetValues<PlayerRngType>().Select(type => player.PlayerRng.GetRng(type).Counter).ToArray(),
            player.Creature.CurrentHp,
            player.Gold,
            SnapshotFactory.SnapshotDeck(player).ToArray(),
            SnapshotFactory.SnapshotRelics(player).ToArray(),
            SnapshotFactory.SnapshotPotions(player).ToArray(),
            callbacks.ToArray(),
            (probe.BeforeCombatStartCount, probe.VictoryCount, probe.CombatEndCount, probe.BeforePotionCount, probe.AfterPotionCount),
            act.CreatedMonster is CombatReportingMonster reportingMonster
                ? reportingMonster.PerformedMoveCount
                : 0,
            player.PlayerCombatState is null,
            player.Creature.CombatState is null,
            combatLogs,
            manifest!);
    }

    private static void ConfigureResolvedCardDeck(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }

        AddToDeck<Volley>(player);
        AddToDeck<StrikeRegent>(player);
    }

    private static void ConfigureAutoplayDeck(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }

        for (int i = 0; i < 8; i++)
        {
            AddToDeck<StrikeRegent>(player);
        }
    }

    private static void ConfigureDazedOpeningDeck(Player player)
    {
        ClearDeck(player);
        AddDazed(player, 5);
    }

    private static void ConfigureMayhemDeck(Player player)
    {
        ConfigureDazedOpeningDeck(player);
        AddToDeck<StrikeRegent>(player);
    }

    private static void ConfigureMayhemCombat(Player player) => PlaceDrawOrder(
        player,
        player.PlayerCombatState!.DrawPile.Cards.OfType<Dazed>().Cast<CardModel>()
            .Concat(player.PlayerCombatState.DrawPile.Cards.OfType<StrikeRegent>())
            .ToArray());

    private static void ConfigureHookPlayDeck(Player player) => ConfigureMayhemDeck(player);

    private static void ConfigureHookPlayCombat(Player player) => ConfigureMayhemCombat(player);

    private static Type NestedParentType(string scenario) => scenario switch
    {
        nameof(BeatDown) => typeof(BeatDown),
        nameof(Catastrophe) => typeof(Catastrophe),
        nameof(DecisionsDecisions) => typeof(DecisionsDecisions),
        nameof(IAmInvincible) => typeof(DefendRegent),
        _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown ownership scenario."),
    };

    private static void ConfigureNestedOwnershipDeck(Player player, string scenario)
    {
        ClearDeck(player);
        switch (scenario)
        {
            case nameof(BeatDown):
                AddToDeck<BeatDown>(player);
                AddDazed(player, 4);
                AddCards<StrikeRegent>(player, 3);
                break;
            case nameof(Catastrophe):
                AddToDeck<Catastrophe>(player);
                AddDazed(player, 4);
                AddCards<StrikeRegent>(player, 2);
                break;
            case nameof(DecisionsDecisions):
                AddToDeck<DecisionsDecisions>(player);
                AddToDeck<CombatReportingNestedSkillCard>(player);
                AddDazed(player, 3);
                break;
            case nameof(IAmInvincible):
                AddToDeck<DefendRegent>(player);
                AddDazed(player, 4);
                AddToDeck<IAmInvincible>(player);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown ownership scenario.");
        }
    }

    private static void ConfigureNestedOwnershipCombat(Player player, string scenario)
    {
        CardPile draw = player.PlayerCombatState!.DrawPile;
        switch (scenario)
        {
            case nameof(BeatDown):
                foreach (StrikeRegent strike in draw.Cards.OfType<StrikeRegent>().ToArray())
                {
                    CardPileCmd.Add(strike, PileType.Discard);
                }

                PlaceDrawOrder(
                    player,
                    draw.Cards.OfType<BeatDown>().Cast<CardModel>()
                        .Concat(draw.Cards.OfType<Dazed>())
                        .ToArray());
                break;
            case nameof(Catastrophe):
                PlaceDrawOrder(
                    player,
                    draw.Cards.OfType<Catastrophe>().Cast<CardModel>()
                        .Concat(draw.Cards.OfType<Dazed>())
                        .Concat(draw.Cards.OfType<StrikeRegent>())
                        .ToArray());
                break;
            case nameof(DecisionsDecisions):
                DecisionsDecisions decisions = Assert.Single(draw.Cards.OfType<DecisionsDecisions>());
                decisions.MakeTemporaryFreeThisTurn();
                PlaceDrawOrder(
                    player,
                    new CardModel[] { decisions }
                        .Concat(draw.Cards.OfType<CombatReportingNestedSkillCard>())
                        .Concat(draw.Cards.OfType<Dazed>())
                        .ToArray());
                break;
            case nameof(IAmInvincible):
                PlaceDrawOrder(
                    player,
                    draw.Cards.OfType<DefendRegent>().Cast<CardModel>()
                        .Concat(draw.Cards.OfType<Dazed>())
                        .Concat(draw.Cards.OfType<IAmInvincible>())
                        .ToArray());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown ownership scenario.");
        }
    }

    private static void ConfigureDistilledChaosOwnershipDeck(Player player)
    {
        ClearDeck(player);
        AddDazed(player, 5);
        AddCards<StrikeRegent>(player, 3);
    }

    private static void ConfigureDistilledChaosOwnershipCombat(Player player) => PlaceDrawOrder(
        player,
        player.PlayerCombatState!.DrawPile.Cards.OfType<Dazed>().Cast<CardModel>()
            .Concat(player.PlayerCombatState.DrawPile.Cards.OfType<StrikeRegent>())
            .ToArray());

    private static void ConfigureReplayOwnershipDeck(Player player)
    {
        ClearDeck(player);
        AddToDeck<StrikeRegent>(player);
        AddDazed(player, 4);
    }

    private static void ConfigureReplayOwnershipCombat(Player player)
    {
        StrikeRegent strike = Assert.Single(player.PlayerCombatState!.DrawPile.Cards.OfType<StrikeRegent>());
        strike.MakeTemporaryFreeThisTurn();
        strike.BaseReplayCount = 2;
        PlaceDrawOrder(
            player,
            new CardModel[] { strike }
                .Concat(player.PlayerCombatState.DrawPile.Cards.OfType<Dazed>())
                .ToArray());
    }

    private static void ConfigureXOwnershipDeck(Player player)
    {
        ClearDeck(player);
        AddToDeck<Volley>(player);
        AddDazed(player, 4);
    }

    private static void ConfigureXOwnershipCombat(Player player)
    {
        Volley volley = Assert.Single(player.PlayerCombatState!.DrawPile.Cards.OfType<Volley>());
        PlaceDrawOrder(
            player,
            new CardModel[] { volley }
                .Concat(player.PlayerCombatState.DrawPile.Cards.OfType<Dazed>())
                .ToArray());
    }

    private static void PlaceDrawOrder(Player player, params CardModel[] ordered)
    {
        foreach (CardModel card in ordered.Reverse())
        {
            CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
        }
    }

    private static void ClearDeck(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }
    }

    private static void AddDazed(Player player, int count) => AddCards<Dazed>(player, count);

    private static void AddCards<TCard>(Player player, int count)
        where TCard : CardModel
    {
        for (int i = 0; i < count; i++)
        {
            AddToDeck<TCard>(player);
        }
    }

    private static TCard AddToDeck<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Deck);
        return card;
    }

    private sealed record RunCapture(
        (bool Won, bool ReachedBoss, int FloorsVisited, int FinalPlayerHp) Result,
        string[] Decisions,
        int[] RunRngCounters,
        int[] PlayerRngCounters,
        int PlayerHp,
        int PlayerGold,
        string[] Deck,
        string[] Relics,
        string?[] Potions,
        string[] RoomCallbacks,
        (int BeforeCombat, int Victory, int CombatEnd, int BeforePotion, int AfterPotion) HookCounts,
        int MonsterMoveCount,
        bool PlayerCombatStateCleared,
        bool CreatureCombatStateCleared,
        IReadOnlyDictionary<string, CombatLog> CombatLogs,
        RunManifest Manifest);

    private class RecordingDecisionSource : IRunDecisionSource
    {
        public List<string> Calls { get; } = [];

        public virtual Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options)
        {
            MapPoint selected = options.OrderBy(point => point.coord.col).First();
            Calls.Add($"map:{selected.coord}");
            return Task.FromResult(selected);
        }

        public virtual Task<CombatDecision> ChooseCombatActionAsync(Sts2Sim.Core.Combat.CombatState state)
        {
            Player player = state.Players[0];
            foreach (CardModel card in player.PlayerCombatState!.Hand.Cards)
            {
                if (!card.CanPlay(out _))
                {
                    continue;
                }

                Creature? target = card.TargetType == TargetType.AnyEnemy
                    ? state.HittableEnemies.FirstOrDefault()
                    : null;
                Calls.Add($"play:{card.Id}:{target?.SlotName ?? target?.Monster?.Id.ToString() ?? "none"}");
                return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
            }

            Calls.Add("end-turn");
            return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
        }

        public virtual Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
        {
            RewardDecision decision = !rewards.Gold.IsResolved ? new RewardDecision.TakeGold() :
                rewards.Potion is { IsResolved: false } ? new RewardDecision.TakePotion() :
                rewards.Relic is { IsResolved: false } ? new RewardDecision.TakeRelic() :
                !rewards.Card.IsResolved ? rewards.Card.Options.Count > 0
                    ? new RewardDecision.TakeCard(rewards.Card.Options[0])
                    : new RewardDecision.SkipCard() :
                rewards.ExtraRewards.FirstOrDefault(reward => !reward.IsResolved) is { } extra
                    ? new RewardDecision.ResolveExtra(extra, (extra as CardReward)?.Options.FirstOrDefault())
                    : new RewardDecision.Done();
            Calls.Add($"reward:{decision.GetType().Name}");
            return Task.FromResult(decision);
        }
    }

    private sealed class CardTypeDecisionSource(Type cardType) : RecordingDecisionSource
    {
        private bool _played;

        public override Task<CombatDecision> ChooseCombatActionAsync(Sts2Sim.Core.Combat.CombatState state)
        {
            if (!_played)
            {
                CardModel? card = state.Players[0].PlayerCombatState!.Hand.Cards
                    .FirstOrDefault(candidate => candidate.GetType() == cardType);
                if (card is not null)
                {
                    _played = true;
                    Creature? target = card.TargetType == TargetType.AnyEnemy
                        ? state.HittableEnemies.Single()
                        : null;
                    Calls.Add($"play:{card.Id}");
                    return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
                }
            }

            Calls.Add("end-turn");
            return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
        }
    }

    private sealed class ResolvedCardDecisionSource : RecordingDecisionSource
    {
        public override Task<CombatDecision> ChooseCombatActionAsync(Sts2Sim.Core.Combat.CombatState state)
        {
            Player player = state.Players[0];
            CardModel? volley = player.PlayerCombatState!.Hand.Cards.OfType<Volley>().FirstOrDefault();
            if (volley is not null)
            {
                Calls.Add("play:volley");
                return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(volley, null));
            }

            StrikeRegent? strike = player.PlayerCombatState.Hand.Cards.OfType<StrikeRegent>().FirstOrDefault();
            if (strike is not null)
            {
                strike.MakeTemporaryFreeThisTurn();
                strike.BaseReplayCount = 2;
                Calls.Add("play:free-replay-strike");
                return Task.FromResult<CombatDecision>(
                    new CombatDecision.PlayCard(strike, state.HittableEnemies.First()));
            }

            Calls.Add("end-turn");
            return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
        }
    }

    private sealed class EndTurnDecisionSource : RecordingDecisionSource
    {
        public override Task<CombatDecision> ChooseCombatActionAsync(Sts2Sim.Core.Combat.CombatState state)
        {
            Calls.Add("end-turn");
            return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
        }
    }
}

file static class RunManifestTestExtensions
{
    public static bool Won(this RunManifest manifest) => manifest.Result == "victory";
}
