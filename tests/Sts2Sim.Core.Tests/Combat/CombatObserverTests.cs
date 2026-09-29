using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Combat;

file sealed class ObserverBracketCard : CardModel
{
    public InvalidOperationException? Failure { get; set; }

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay) => TakeFailure();

    private Task TakeFailure()
    {
        InvalidOperationException? failure = Failure;
        Failure = null;
        return failure is null ? Task.CompletedTask : Task.FromException(failure);
    }
}

file sealed class ObserverBracketPotion : PotionModel
{
    public InvalidOperationException? Failure { get; set; }

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    protected override Task OnUse(Creature? target)
    {
        InvalidOperationException? failure = Failure;
        Failure = null;
        return failure is null ? Task.CompletedTask : Task.FromException(failure);
    }
}

file sealed class ObserverBracketMonster : MonsterModel
{
    public InvalidOperationException? Failure { get; set; }

    public override int MinInitialHp => 100;

    public override int MaxInitialHp => 100;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("OBSERVER_BRACKET_MOVE", PerformMove);
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }

    private Task PerformMove(IReadOnlyList<Creature> targets)
    {
        InvalidOperationException? failure = Failure;
        Failure = null;
        return failure is null ? Task.CompletedTask : Task.FromException(failure);
    }
}

[Collection("ModelDb")]
public sealed class CombatObserverTests : IDisposable
{
    public CombatObserverTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(ObserverBracketCard))
                .Append(typeof(ObserverBracketPotion))
                .Append(typeof(ObserverBracketMonster)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Observer_ReportsExactDrawMoveAndTurnBoundariesInLifecycleOrder()
    {
        (RunState runState, CombatRoom room) = CreateRoom("combat-observer-order");
        var observer = new RecordingCombatObserver();
        room.ConfigureObserver(observer);

        await room.EnterInternal(runState);

        Assert.Equal("combat-started", observer.Events[0]);
        Assert.Equal(5, observer.Events.Count(entry => entry.StartsWith("draw:", StringComparison.Ordinal)));
        Assert.Equal("player-turn-started", observer.Events[^1]);

        int firstTurnEventCount = observer.Events.Count;
        await room.Engine.EndPlayerTurnAsync();

        string[] completed = observer.Events.Skip(firstTurnEventCount).ToArray();
        int beforeMove = Array.FindIndex(completed, entry => entry.StartsWith("enemy-before:", StringComparison.Ordinal));
        int afterMove = Array.FindIndex(completed, entry => entry.StartsWith("enemy-after:", StringComparison.Ordinal));
        int turnEnded = Array.IndexOf(completed, "player-turn-ended");
        int firstNextDraw = Array.FindIndex(completed, entry => entry.StartsWith("draw:", StringComparison.Ordinal));
        int nextTurnStarted = Array.LastIndexOf(completed, "player-turn-started");

        Assert.True(beforeMove >= 0);
        Assert.Equal(beforeMove + 1, afterMove);
        Assert.Equal(completed[beforeMove]["enemy-before:".Length..], completed[afterMove]["enemy-after:".Length..]);
        Assert.True(afterMove < turnEnded);
        Assert.True(turnEnded < firstNextDraw);
        Assert.Equal(5, completed.Count(entry => entry.StartsWith("draw:", StringComparison.Ordinal)));
        Assert.True(firstNextDraw < nextTurnStarted);
    }

    [Fact]
    public async Task Observer_ReportsExactDrawOrderAcrossReshuffle()
    {
        (RunState runState, CombatRoom room) = CreateRoom("combat-observer-reshuffle");
        var observer = new RecordingCombatObserver();
        room.ConfigureObserver(observer);
        await room.EnterInternal(runState);

        Player player = runState.Players[0];
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards
                     .Concat(player.PlayerCombatState.DrawPile.Cards)
                     .Concat(player.PlayerCombatState.DiscardPile.Cards)
                     .ToArray())
        {
            CardPileCmd.Remove(card);
        }

        var first = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        first.AssignOwner(player);
        CardPileCmd.Add(first, PileType.Draw);
        var second = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        second.AssignOwner(player);
        CardPileCmd.Add(second, PileType.Discard);
        int eventCountBeforeDraw = observer.Events.Count;

        await CardPileCmd.Draw(room.Engine.State, 2, player, fromHandDraw: false);

        Assert.Equal(
            [$"draw:{first.Id}", $"draw:{second.Id}"],
            observer.Events.Skip(eventCountBeforeDraw));
    }

    [Fact]
    public async Task InvalidPotionTarget_DoesNotEmitObserverCallbacksOrConsumePotion()
    {
        (RunState runState, CombatRoom room) = CreateRoom("combat-observer-invalid-potion");
        var observer = new RecordingCombatObserver();
        room.ConfigureObserver(observer);
        await room.EnterInternal(runState);

        Player player = runState.Players[0];
        var potion = (FirePotion)ModelDb.Potion<FirePotion>().MutableClone();
        player.AddPotionInternal(potion);
        int potionEventCountBeforeUse = observer.Events.Count(entry =>
            entry.StartsWith("potion-", StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PotionCmd.Use(potion, player, player.Creature));

        Assert.Contains(potion, player.PotionSlots);
        Assert.Equal(
            potionEventCountBeforeUse,
            observer.Events.Count(entry => entry.StartsWith("potion-", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task PotionPolicy_DoesNotUseAnyPotionWithoutAHittableEnemy()
    {
        (RunState runState, CombatRoom room) = CreateRoom("combat-potion-policy-no-target");
        await room.EnterInternal(runState);

        Player player = runState.Players[0];
        var potion = (StrengthPotion)ModelDb.Potion<StrengthPotion>().MutableClone();
        player.AddPotionInternal(potion);
        Creature enemy = room.Engine.State.Enemies[0];
        enemy.LoseHpInternal(enemy.CurrentHp, Sts2Sim.Core.ValueProps.ValueProp.Unblockable);

        bool used = await CombatPotionPolicy.TryUseFirstAvailableAsync(room.Engine, player);

        Assert.False(used);
        Assert.Contains(potion, player.PotionSlots);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingEnemyMove_PreservesUnderlyingException(bool attachObserver)
    {
        (RunState runState, CombatRoom room) = CreateRoom("combat-observer-missing-move");
        RecordingCombatObserver? observer = attachObserver ? new RecordingCombatObserver() : null;
        if (observer is not null)
        {
            room.ConfigureObserver(observer);
        }
        await room.EnterInternal(runState);

        MonsterModel monster = room.Engine.State.Enemies[0].Monster!;
        typeof(MonsterModel).GetProperty(nameof(MonsterModel.NextMove))!.SetValue(monster, null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => room.Engine.EndPlayerTurnAsync());

        Assert.Equal("No move has been rolled.", exception.Message);
        if (observer is not null)
        {
            Assert.DoesNotContain(
                observer.Events,
                entry => entry.StartsWith("enemy-before:", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task ObserverException_PropagatesUnchangedFromCombatLifecycle()
    {
        (RunState runState, CombatRoom room) = CreateRoom("combat-observer-exception");
        var expected = new InvalidOperationException("observer failure");
        room.ConfigureObserver(new ThrowingCombatObserver(expected));

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => room.EnterInternal(runState));

        Assert.Same(expected, actual);
    }

    [Theory]
    [InlineData(ObservedAction.Card)]
    [InlineData(ObservedAction.Potion)]
    [InlineData(ObservedAction.Enemy)]
    public async Task FailedCoreAction_AbortsObserverScopeAndPreservesOriginalException(
        ObservedAction action)
    {
        var expected = new InvalidOperationException($"{action} core failure");
        var observer = new BracketTrackingObserver(action);

        InvalidOperationException actual = await ExerciseFailedThenSuccessfulAction(
            action,
            observer,
            expected);

        Assert.Same(expected, actual);
        Assert.Equal(1, observer.AbortCount);
        Assert.Equal(0, observer.ActiveDepth);
        Assert.Equal(1, observer.SuccessCount);
    }

    [Theory]
    [InlineData(ObservedAction.Card, ObserverCallback.Start)]
    [InlineData(ObservedAction.Card, ObserverCallback.Finish)]
    [InlineData(ObservedAction.Potion, ObserverCallback.Start)]
    [InlineData(ObservedAction.Potion, ObserverCallback.Finish)]
    [InlineData(ObservedAction.Enemy, ObserverCallback.Start)]
    [InlineData(ObservedAction.Enemy, ObserverCallback.Finish)]
    public async Task ObserverCallbackFailure_AbortsScopeAndPreservesCallbackException(
        ObservedAction action,
        ObserverCallback callback)
    {
        var expected = new InvalidOperationException($"{action} {callback} failure");
        var observer = new BracketTrackingObserver(action, callback, expected);

        InvalidOperationException actual = await ExerciseFailedThenSuccessfulAction(
            action,
            observer,
            coreFailure: null);

        Assert.Same(expected, actual);
        Assert.Equal(1, observer.AbortCount);
        Assert.Equal(0, observer.ActiveDepth);
        Assert.Equal(1, observer.SuccessCount);
    }

    [Theory]
    [InlineData(ObservedAction.Card)]
    [InlineData(ObservedAction.Potion)]
    [InlineData(ObservedAction.Enemy)]
    public async Task AbortCallbackFailure_DoesNotReplaceOriginalCoreException(
        ObservedAction action)
    {
        var expected = new InvalidOperationException($"{action} core failure");
        var abortFailure = new InvalidOperationException($"{action} abort failure");
        var observer = new BracketTrackingObserver(
            action,
            abortFailure: abortFailure);

        InvalidOperationException actual = await ExerciseFailedThenSuccessfulAction(
            action,
            observer,
            expected);

        Assert.Same(expected, actual);
        Assert.Equal(1, observer.AbortCount);
        Assert.Equal(0, observer.ActiveDepth);
        Assert.Equal(1, observer.SuccessCount);
    }

    private static async Task<InvalidOperationException> ExerciseFailedThenSuccessfulAction(
        ObservedAction action,
        BracketTrackingObserver observer,
        InvalidOperationException? coreFailure)
    {
        var runState = new RunState($"observer-bracket-{action}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (ObserverBracketMonster)ModelDb.Monster<ObserverBracketMonster>().MutableClone());
        room.ConfigureObserver(observer);
        await room.EnterInternal(runState);
        Creature enemy = room.Engine.State.Enemies.Single();

        Task firstAction;
        Func<Task> successfulAction;
        switch (action)
        {
            case ObservedAction.Card:
                {
                    CardModel first = CreateCard(player, coreFailure);
                    firstAction = first.PlayAsync(enemy);
                    successfulAction = async () =>
                    {
                        await CreateCard(player, failure: null).PlayAsync(enemy);
                    };
                    break;
                }
            case ObservedAction.Potion:
                {
                    PotionModel first = CreatePotion(player, coreFailure);
                    firstAction = PotionCmd.Use(first, player, player.Creature);
                    successfulAction = () => PotionCmd.Use(
                        CreatePotion(player, failure: null),
                        player,
                        player.Creature);
                    break;
                }
            case ObservedAction.Enemy:
                {
                    ((ObserverBracketMonster)enemy.Monster!).Failure = coreFailure;
                    firstAction = room.Engine.EndPlayerTurnAsync();
                    successfulAction = room.Engine.EndPlayerTurnAsync;
                    break;
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => firstAction);
        await successfulAction();
        return actual;
    }

    private static CardModel CreateCard(
        Player player,
        InvalidOperationException? failure)
    {
        var card = (ObserverBracketCard)ModelDb.Card<ObserverBracketCard>().MutableClone();
        card.AssignOwner(player);
        card.Failure = failure;
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static PotionModel CreatePotion(
        Player player,
        InvalidOperationException? failure)
    {
        var potion = (ObserverBracketPotion)ModelDb.Potion<ObserverBracketPotion>().MutableClone();
        potion.Failure = failure;
        player.AddPotionInternal(potion);
        return potion;
    }

    private static (RunState RunState, CombatRoom Room) CreateRoom(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        return (runState, room);
    }

    private class RecordingCombatObserver : ICombatObserver
    {
        public List<string> Events { get; } = [];

        public virtual void CombatStarted(CombatState state) => Events.Add("combat-started");

        public void PlayerTurnStarted(CombatState state) => Events.Add("player-turn-started");

        public void CardDrawn(CardModel card) => Events.Add($"draw:{card.Id}");

        public void CardPlayStarted(CardModel card, Creature? target) =>
            Events.Add($"autoplay-before:{card.Id}");

        public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay) =>
            Events.Add($"autoplay-after:{card.Id}");

        public void EnemyMoveStarted(Creature source, string moveId) =>
            Events.Add($"enemy-before:{moveId}");

        public void EnemyMoveFinished(Creature source, string moveId) =>
            Events.Add($"enemy-after:{moveId}");

        public void PlayerTurnEnded(CombatState state) => Events.Add("player-turn-ended");

        public void PotionUseStarted(PotionModel potion, Creature? target) =>
            Events.Add($"potion-before:{potion.Id}");

        public void PotionUseFinished(PotionModel potion, Creature? target) =>
            Events.Add($"potion-after:{potion.Id}");
    }

    private sealed class ThrowingCombatObserver : RecordingCombatObserver
    {
        private readonly InvalidOperationException _exception;

        public ThrowingCombatObserver(InvalidOperationException exception)
        {
            _exception = exception;
        }

        public override void CombatStarted(CombatState state) => throw _exception;
    }

    public enum ObservedAction
    {
        Card,
        Potion,
        Enemy,
    }

    public enum ObserverCallback
    {
        None,
        Start,
        Finish,
    }

    private sealed class BracketTrackingObserver : ICombatObserver
    {
        private readonly ObservedAction _action;
        private readonly ObserverCallback _failingCallback;
        private readonly InvalidOperationException? _callbackFailure;
        private readonly InvalidOperationException? _abortFailure;
        private bool _callbackHasFailed;

        public BracketTrackingObserver(
            ObservedAction action,
            ObserverCallback failingCallback = ObserverCallback.None,
            InvalidOperationException? callbackFailure = null,
            InvalidOperationException? abortFailure = null)
        {
            _action = action;
            _failingCallback = failingCallback;
            _callbackFailure = callbackFailure;
            _abortFailure = abortFailure;
        }

        public int ActiveDepth { get; private set; }

        public int AbortCount { get; private set; }

        public int SuccessCount { get; private set; }

        public void CombatStarted(CombatState state)
        {
        }

        public void PlayerTurnStarted(CombatState state)
        {
        }

        public void CardDrawn(CardModel card)
        {
        }

        public void CardPlayStarted(CardModel card, Creature? target) =>
            Start(ObservedAction.Card);

        public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay) =>
            Finish(ObservedAction.Card);

        public void CardPlayAborted(CardModel card, Creature? target) =>
            Abort(ObservedAction.Card);

        public void EnemyMoveStarted(Creature source, string moveId) =>
            Start(ObservedAction.Enemy);

        public void EnemyMoveFinished(Creature source, string moveId) =>
            Finish(ObservedAction.Enemy);

        public void EnemyMoveAborted(Creature source, string moveId) =>
            Abort(ObservedAction.Enemy);

        public void PlayerTurnEnded(CombatState state)
        {
        }

        public void PotionUseStarted(PotionModel potion, Creature? target) =>
            Start(ObservedAction.Potion);

        public void PotionUseFinished(PotionModel potion, Creature? target) =>
            Finish(ObservedAction.Potion);

        public void PotionUseAborted(PotionModel potion, Creature? target) =>
            Abort(ObservedAction.Potion);

        private void Start(ObservedAction action)
        {
            if (action != _action)
            {
                return;
            }

            ActiveDepth++;
            ThrowCallbackOnce(ObserverCallback.Start);
        }

        private void Finish(ObservedAction action)
        {
            if (action != _action)
            {
                return;
            }

            ThrowCallbackOnce(ObserverCallback.Finish);
            ActiveDepth--;
            SuccessCount++;
        }

        private void Abort(ObservedAction action)
        {
            if (action != _action)
            {
                return;
            }

            ActiveDepth--;
            AbortCount++;
            if (_abortFailure is not null)
            {
                throw _abortFailure;
            }
        }

        private void ThrowCallbackOnce(ObserverCallback callback)
        {
            if (!_callbackHasFailed && _failingCallback == callback)
            {
                _callbackHasFailed = true;
                throw _callbackFailure!;
            }
        }
    }
}
