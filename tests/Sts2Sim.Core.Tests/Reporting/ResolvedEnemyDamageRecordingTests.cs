using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Reporting;

file sealed class DoubleLethalReportingMonster : MonsterModel
{
    public override int MinInitialHp => 100;

    public override int MaxInitialHp => 100;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("DOUBLE_LETHAL", PerformDoubleLethal);
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }

    private async Task PerformDoubleLethal(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(999m).FromMonster(this).Execute();
        await DamageCmd.Attack(999m).FromMonster(this).Execute();
    }
}

[Collection("ModelDb")]
public sealed class ResolvedEnemyDamageRecordingTests : IDisposable
{
    public ResolvedEnemyDamageRecordingTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(DoubleLethalReportingMonster)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EnemyAction_PreservesResolvedHitOrderAcrossFairyThenLizardTail()
    {
        var runState = new RunState("resolved-enemy-damage", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        await RelicCmd.Obtain(ModelDb.Relic<LizardTail>(), player);
        var fairy = (FairyInABottle)ModelDb.Potion<FairyInABottle>().MutableClone();
        player.AddPotionInternal(fairy);
        var room = new CombatRoom(
            () => (DoubleLethalReportingMonster)ModelDb
                .Monster<DoubleLethalReportingMonster>()
                .MutableClone());
        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        var observer = new CombatRecordingObserver(recorder, runState, room);
        room.ConfigureObserver(observer);
        await room.EnterInternal(runState);

        await room.Engine.EndPlayerTurnAsync();

        observer.CaptureFinalState();
        observer.EndCombat(victory: false, []);
        recorder.ExitFloor();
        recorder.EndRun(
            won: false,
            floorsVisited: 1,
            finalHp: player.Creature.CurrentHp);

        CombatLog log = Assert.Single(recorder.CombatLogs).Value;
        TurnRecord turn = Assert.Single(log.Turns, candidate =>
            candidate.Actions.OfType<EnemyAction>().Any());
        ActionRecord[] actions = turn.Actions.ToArray();
        EnemyAction enemyAction = Assert.IsType<EnemyAction>(actions[0]);
        Assert.Equal("DOUBLE_LETHAL", enemyAction.MoveId);
        Assert.Collection(
            enemyAction.DamageToTargets,
            first => Assert.Equal(new DamageTakenRecord(75, 0), first),
            second => Assert.Equal(new DamageTakenRecord(22, 0), second));
        UsePotionAction potionAction = Assert.IsType<UsePotionAction>(actions[1]);
        Assert.Equal(ModelDb.GetId<FairyInABottle>().ToString(), potionAction.Potion);
        Assert.Equal(37, turn.PlayerPost.Hp);
    }

    [Theory]
    [InlineData(RecordedAction.Card)]
    [InlineData(RecordedAction.Potion)]
    [InlineData(RecordedAction.Enemy)]
    public async Task AbortedConcreteObserverScope_AllowsFollowingSuccessfulAction(
        RecordedAction action)
    {
        var runState = new RunState($"concrete-observer-abort-{action}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        var recorder = new RunRecorder(() => DateTimeOffset.UnixEpoch);
        recorder.BeginRun(runState);
        recorder.EnterFloor(
            new MapPoint(0, 0) { PointType = MapPointType.Monster },
            RoomType.Monster);
        var observer = new CombatRecordingObserver(recorder, runState, room);
        room.ConfigureObserver(observer);
        await room.EnterInternal(runState);
        Creature enemy = room.Engine.State.Enemies.Single();

        switch (action)
        {
            case RecordedAction.Card:
                {
                    var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
                    card.AssignOwner(player);
                    CardPileCmd.Add(card, PileType.Hand);
                    observer.CardPlayStarted(card, enemy);
                    observer.CardPlayAborted(card, enemy);
                    await card.PlayAsync(enemy);
                    break;
                }
            case RecordedAction.Potion:
                {
                    var potion = (StrengthPotion)ModelDb.Potion<StrengthPotion>().MutableClone();
                    player.AddPotionInternal(potion);
                    observer.PotionUseStarted(potion, player.Creature);
                    observer.PotionUseAborted(potion, player.Creature);
                    await PotionCmd.Use(potion, player, player.Creature);
                    break;
                }
            case RecordedAction.Enemy:
                {
                    string moveId = enemy.Monster!.NextMove!.StateId;
                    observer.EnemyMoveStarted(enemy, moveId);
                    observer.EnemyMoveAborted(enemy, moveId);
                    await room.Engine.EndPlayerTurnAsync();
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }

        observer.CaptureFinalState();
        observer.EndCombat(victory: false, []);
        recorder.ExitFloor();
        recorder.EndRun(
            won: false,
            floorsVisited: 1,
            finalHp: player.Creature.CurrentHp);

        IEnumerable<ActionRecord> actions = Assert.Single(recorder.CombatLogs).Value.Turns
            .SelectMany(turn => turn.Actions);
        switch (action)
        {
            case RecordedAction.Card:
                Assert.Single(actions.OfType<PlayCardAction>());
                break;
            case RecordedAction.Potion:
                Assert.Single(actions.OfType<UsePotionAction>());
                break;
            case RecordedAction.Enemy:
                Assert.Single(actions.OfType<EnemyAction>());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    public enum RecordedAction
    {
        Card,
        Potion,
        Enemy,
    }
}
