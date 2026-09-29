using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

file sealed class OffersGoldEvent : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        new[]
        {
            new EventOption("TAKE_GOLD", () =>
            {
                OfferRewards(RewardsSet.CreateCustom(Owner, gold: new GoldReward(50, Owner)));
                Finish();
                return Task.CompletedTask;
            }),
        };
}

file sealed class QueuesRewardsEvent : EventModel
{
    private IReadOnlyList<RewardsSet> _rewards = Array.Empty<RewardsSet>();

    public void SetRewards(params RewardsSet[] rewards)
    {
        AssertMutable();
        _rewards = rewards;
    }

    public void QueueGold(int amount) =>
        OfferRewards(RewardsSet.CreateCustom(Owner, gold: new GoldReward(amount, Owner)));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        new[]
        {
            new EventOption("QUEUE_ALL", () =>
            {
                foreach (RewardsSet rewards in _rewards)
                {
                    OfferRewards(rewards);
                }

                Finish();
                return Task.CompletedTask;
            }),
        };
}

file sealed class EnqueueReward : TakeableReward
{
    private readonly EventModel _event;
    private readonly IRunState _runState;
    private readonly RewardsSet _reward;

    public EnqueueReward(
        Player player,
        EventModel @event,
        IRunState runState,
        RewardsSet reward) : base(player)
    {
        _event = @event;
        _runState = runState;
        _reward = reward;
    }

    public override void Populate(IRunState runState)
    {
    }

    protected override Task OnTake()
    {
        Assert.True(_event.TryOfferRewardsFromCurrentEvent(_runState, _reward));
        return Task.CompletedTask;
    }
}

file sealed class RecordingRewardDecisionSource : IRunDecisionSource
{
    private readonly List<RewardsSet> _seenRewards = new();

    public IReadOnlyList<RewardsSet> SeenRewards => _seenRewards;

    public int SkipCardDecisionCount { get; private set; }

    public int DoneDecisionCount { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[0]);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
    {
        if (_seenRewards.Count == 0 || !ReferenceEquals(_seenRewards[^1], rewards))
        {
            _seenRewards.Add(rewards);
        }

        if (!rewards.Gold.IsResolved)
        {
            return Task.FromResult<RewardDecision>(new RewardDecision.TakeGold());
        }

        Reward? extra = rewards.ExtraRewards.FirstOrDefault(reward => !reward.IsResolved);
        if (extra is not null)
        {
            return Task.FromResult<RewardDecision>(new RewardDecision.ResolveExtra(extra));
        }

        if (!rewards.Card.IsResolved)
        {
            SkipCardDecisionCount++;
            return Task.FromResult<RewardDecision>(new RewardDecision.SkipCard());
        }

        DoneDecisionCount++;
        return Task.FromResult<RewardDecision>(new RewardDecision.Done());
    }
}

public sealed class EventRewardOfferTests : IDisposable
{
    public EventRewardOfferTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(Sts2Sim.Core.Models.Cards.FallingStar),
            typeof(Sts2Sim.Core.Models.Cards.Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RunEngine_DrainsFinishedEventFixedGoldOfferWithoutAddingAPhantomCard()
    {
        (RunState runState, Player player) = CreateRunWithPlayer("event-reward-engine");
        int deckBefore = player.Deck.Cards.Count;
        decimal goldBefore = player.Gold;
        EventRoom room = await EnterRoom(runState, new OffersGoldEvent());

        await RunEngine.DriveEventToCompletion(room);

        Assert.Equal(goldBefore + 50m, player.Gold);
        Assert.Equal(deckBefore, player.Deck.Cards.Count);
        Assert.False(room.Event.HasPendingRewardOffers);
    }

    [Fact]
    public async Task RunDriver_DrainsFixedGoldOfferAndReachesDoneWithoutSkippingAPhantomCard()
    {
        (RunState runState, Player player) = CreateRunWithPlayer("event-reward-driver");
        int deckBefore = player.Deck.Cards.Count;
        decimal goldBefore = player.Gold;
        EventRoom room = await EnterRoom(runState, new OffersGoldEvent());
        var decisions = new RecordingRewardDecisionSource();
        var driver = new RunDriver(runState, decisions);

        await driver.DriveEventAsync(room);

        Assert.Equal(goldBefore + 50m, player.Gold);
        Assert.Equal(deckBefore, player.Deck.Cards.Count);
        Assert.Equal(0, decisions.SkipCardDecisionCount);
        Assert.Equal(1, decisions.DoneDecisionCount);
        Assert.False(room.Event.HasPendingRewardOffers);
    }

    [Fact]
    public async Task RunDriver_ResumesParentAfterNestedOfferBeforeNextQueuedOffer()
    {
        (RunState runState, Player player) = CreateRunWithPlayer("event-reward-fifo");
        var prototype = new QueuesRewardsEvent();
        EventRoom room = await EnterRoom(runState, prototype);
        var @event = Assert.IsType<QueuesRewardsEvent>(room.Event);
        RewardsSet third = RewardsSet.CreateCustom(player, gold: new GoldReward(30, player));
        var enqueueThird = new EnqueueReward(player, @event, runState, third);
        RewardsSet first = RewardsSet.CreateCustom(
            player,
            gold: new GoldReward(10, player),
            extraRewards: new Reward[] { enqueueThird });
        RewardsSet second = RewardsSet.CreateCustom(player, gold: new GoldReward(20, player));
        @event.SetRewards(first, second);
        decimal goldBefore = player.Gold;
        var decisions = new RecordingRewardDecisionSource();
        var driver = new RunDriver(runState, decisions);

        await driver.DriveEventAsync(room);

        Assert.Equal(goldBefore + 60m, player.Gold);
        // The child offer interrupts the first set, which resumes before the second set.
        Assert.Equal(new[] { first, third, first, second }, decisions.SeenRewards);
        Assert.Equal(3, decisions.DoneDecisionCount);
        Assert.False(@event.HasPendingRewardOffers);
    }

    [Fact]
    public async Task ControlledRelicEnqueue_RequiresThisMutableEventToBeTheCurrentRoom()
    {
        (RunState runState, Player player) = CreateRunWithPlayer("event-reward-current-room");
        EventRoom room = await EnterRoom(runState, new QueuesRewardsEvent());
        var @event = Assert.IsType<QueuesRewardsEvent>(room.Event);
        IRunState state = runState;
        RewardsSet accepted = RewardsSet.CreateCustom(player, gold: new GoldReward(5, player));

        Assert.Same(room, state.CurrentRoom);
        Assert.Throws<ArgumentNullException>(
            () => @event.TryOfferRewardsFromCurrentEvent(state, null!));
        Assert.True(@event.TryOfferRewardsFromCurrentEvent(state, accepted));

        Assert.Same(room, runState.PopCurrentRoom());
        Assert.Null(state.CurrentRoom);
        Assert.False(@event.TryOfferRewardsFromCurrentEvent(
            state,
            RewardsSet.CreateCustom(player, gold: new GoldReward(6, player))));
        Assert.True(@event.TryDequeuePendingRewardOffer(out RewardsSet? dequeued));
        Assert.Same(accepted, dequeued);
    }

    [Fact]
    public async Task MutableClone_GetsIndependentEmptyQueueWithoutClearingSourceOffers()
    {
        (RunState runState, _) = CreateRunWithPlayer("event-reward-clone");
        EventRoom room = await EnterRoom(runState, new QueuesRewardsEvent());
        var source = Assert.IsType<QueuesRewardsEvent>(room.Event);
        source.QueueGold(7);

        var clone = (QueuesRewardsEvent)source.MutableClone();

        Assert.True(source.HasPendingRewardOffers);
        Assert.False(clone.HasPendingRewardOffers);
        Assert.True(source.TryDequeuePendingRewardOffer(out RewardsSet? sourceReward));
        Assert.Equal(7, Assert.IsType<RewardsSet>(sourceReward).Gold.Amount);
        Assert.False(clone.TryDequeuePendingRewardOffer(out _));
    }

    private static (RunState RunState, Player Player) CreateRunWithPlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static async Task<EventRoom> EnterRoom(RunState runState, EventModel prototype)
    {
        var room = new EventRoom(() => (EventModel)prototype.MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        return room;
    }
}
