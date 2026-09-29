using System.Threading;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Exceptions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

file sealed class TwoPageTestEvent : EventModel
{
    public int InitialPageCount { get; private set; }

    public int FirstChoiceCount { get; private set; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        InitialPageCount++;
        return new[]
        {
            new EventOption("PAGE_ONE", () =>
            {
                FirstChoiceCount++;
                SetOptions(new[] { new EventOption("PAGE_TWO", () => { Finish(); return Task.CompletedTask; }) });
                return Task.CompletedTask;
            }),
        };
    }
}

file sealed class StaticPageTestEvent : EventModel
{
    public int ChoiceCount { get; private set; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("STAY", () =>
        {
            ChoiceCount++;
            return Task.CompletedTask;
        }),
    };
}
file sealed class ControlledChoiceTestEvent : EventModel
{
    public TaskCompletionSource FirstChoiceStarted { get; private set; } = CreateSignal();

    public TaskCompletionSource ReleaseFirstChoice { get; private set; } = CreateSignal();

    public int FirstChoiceCount;

    protected override void CalculateVars()
    {
        FirstChoiceStarted = CreateSignal();
        ReleaseFirstChoice = CreateSignal();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("FIRST", ChooseFirstAsync),
        new EventOption("OTHER", () => { Finish(); return Task.CompletedTask; }),
    };

    private async Task ChooseFirstAsync()
    {
        Interlocked.Increment(ref FirstChoiceCount);
        FirstChoiceStarted.SetResult();
        await ReleaseFirstChoice.Task;
        Finish();
    }

    private static TaskCompletionSource CreateSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

file sealed class FaultThenRetryTestEvent : EventModel
{
    public TaskCompletionSource FirstChoiceStarted { get; private set; } = CreateSignal();

    public TaskCompletionSource ReleaseFirstFault { get; private set; } = CreateSignal();

    public int AttemptCount { get; private set; }

    protected override void CalculateVars()
    {
        FirstChoiceStarted = CreateSignal();
        ReleaseFirstFault = CreateSignal();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("RETRY", ChooseAsync),
    };

    private async Task ChooseAsync()
    {
        AttemptCount++;
        if (AttemptCount == 1)
        {
            FirstChoiceStarted.SetResult();
            await ReleaseFirstFault.Task;
            throw new InvalidOperationException("Simulated option failure.");
        }

        Finish();
    }

    private static TaskCompletionSource CreateSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

file sealed class CancelThenRetryTestEvent : EventModel
{
    public int AttemptCount { get; private set; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("RETRY", ChooseAsync),
    };

    private Task ChooseAsync()
    {
        AttemptCount++;
        if (AttemptCount == 1)
        {
            return Task.FromCanceled(new CancellationToken(canceled: true));
        }

        Finish();
        return Task.CompletedTask;
    }
}
file sealed class TransitionBeforeCompletionTestEvent : EventModel
{
    public TaskCompletionSource PageTwoOffered { get; private set; } = CreateSignal();

    public TaskCompletionSource ReleaseFirstChoice { get; private set; } = CreateSignal();

    public int PageTwoChoiceCount { get; private set; }

    public int PostBarrierMutationCount { get; private set; }

    protected override void CalculateVars()
    {
        PageTwoOffered = CreateSignal();
        ReleaseFirstChoice = CreateSignal();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("PAGE_ONE", ChoosePageOneAsync),
    };

    private async Task ChoosePageOneAsync()
    {
        SetOptions(new[]
        {
            new EventOption("PAGE_TWO", () =>
            {
                PageTwoChoiceCount++;
                Finish();
                return Task.CompletedTask;
            }),
        });
        PageTwoOffered.SetResult();
        await ReleaseFirstChoice.Task;
        PostBarrierMutationCount++;
    }

    private static TaskCompletionSource CreateSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
[Collection("ModelDb")]
public class EventModelTests : IDisposable
{
    public EventModelTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
            typeof(TwoPageTestEvent), typeof(ControlledChoiceTestEvent), typeof(StaticPageTestEvent),
            typeof(FaultThenRetryTestEvent), typeof(CancelThenRetryTestEvent),
            typeof(TransitionBeforeCompletionTestEvent),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void ModelDbEvent_ReturnsCanonicalTemplate_ThatRejectsLifecycleMutation()
    {
        var run = new RunState("event-canonical", new Overgrowth());
        Player player = CreatePlayer(run);
        TwoPageTestEvent canonical = ModelDb.Event<TwoPageTestEvent>();

        Assert.True(canonical.IsCanonical);
        Assert.Throws<CanonicalModelException>(() => canonical.BeginEvent(run));
        Assert.Throws<CanonicalModelException>(() => canonical.AssignOwner(player));
        Assert.Throws<CanonicalModelException>(() => { _ = canonical.ChooseOption(new EventOption("STRAY", () => Task.CompletedTask)); });
    }

    [Fact]
    public void BeginEvent_BindsAssignedOwnerToItsRun_AndRejectsOwnerFromAnotherRun()
    {
        (RunState ownerRun, Player owner) = CreateRunWithPlayer("event-owner");
        var otherRun = new RunState("event-other-run", new Overgrowth());
        var ev = (TwoPageTestEvent)ModelDb.Event<TwoPageTestEvent>().MutableClone();
        ev.AssignOwner(owner);

        Assert.Throws<InvalidOperationException>(() => ev.BeginEvent(otherRun));

        ev.BeginEvent(ownerRun);
        Assert.Same(owner, ev.Owner);
        Assert.Same(ownerRun, ev.RunState);
    }

    [Fact]
    public async Task BeginEvent_UsesDedicatedDeterministicRng_WithoutAdvancingRunRng_AndResetsState()
    {
        var run = new RunState("event-reset", new Overgrowth());
        var ev = (TwoPageTestEvent)ModelDb.Event<TwoPageTestEvent>().MutableClone();
        int runRngCounterBefore = run.Rng.UnknownMapPoint.Counter;

        ev.BeginEvent(run);
        EventOption firstPageOption = Assert.Single(ev.CurrentOptions);
        int firstRoll = ev.Rng.NextInt(1000);
        await ev.ChooseOption(firstPageOption);
        await ev.ChooseOption(Assert.Single(ev.CurrentOptions));

        ev.BeginEvent(run);

        Assert.Equal(runRngCounterBefore, run.Rng.UnknownMapPoint.Counter);
        Assert.False(ev.IsFinished);
        Assert.Equal(2, ev.InitialPageCount);
        Assert.NotSame(firstPageOption, Assert.Single(ev.CurrentOptions));
        Assert.Equal(firstRoll, ev.Rng.NextInt(1000));
    }

    [Fact]
    public async Task ChooseOption_RequiresTheExactOptionFromTheCurrentPage()
    {
        var run = new RunState("event-option-identity", new Overgrowth());
        var ev = (TwoPageTestEvent)ModelDb.Event<TwoPageTestEvent>().MutableClone();
        ev.BeginEvent(run);
        EventOption firstPageOption = Assert.Single(ev.CurrentOptions);

        Assert.Throws<InvalidOperationException>(() => { _ = ev.ChooseOption(new EventOption(firstPageOption.Key, () => Task.CompletedTask)); });

        await ev.ChooseOption(firstPageOption);
        EventOption secondPageOption = Assert.Single(ev.CurrentOptions);

        Assert.Throws<InvalidOperationException>(() => { _ = ev.ChooseOption(firstPageOption); });

        await ev.ChooseOption(secondPageOption);
        Assert.True(ev.IsFinished);
        Assert.Empty(ev.CurrentOptions);
    }

    [Fact]
    public async Task ChooseOption_ConcurrentSameChoiceSharesOneInvocation_AndRejectsConflictingChoice()
    {
        var run = new RunState("event-concurrent-choice", new Overgrowth());
        var ev = (ControlledChoiceTestEvent)ModelDb.Event<ControlledChoiceTestEvent>().MutableClone();
        ev.BeginEvent(run);
        EventOption first = ev.CurrentOptions.Single(option => option.Key == "FIRST");
        EventOption other = ev.CurrentOptions.Single(option => option.Key == "OTHER");

        Task choice = ev.ChooseOption(first);
        await ev.FirstChoiceStarted.Task;
        Task retry = ev.ChooseOption(first);

        Assert.Same(choice, retry);
        Assert.Throws<InvalidOperationException>(() => { _ = ev.ChooseOption(other); });

        ev.ReleaseFirstChoice.SetResult();
        await Task.WhenAll(choice, retry);

        Assert.Equal(1, ev.FirstChoiceCount);
        Assert.True(ev.IsFinished);
        Assert.Empty(ev.CurrentOptions);
        Assert.Throws<InvalidOperationException>(() => { _ = ev.ChooseOption(first); });
    }

    [Fact]
    public async Task ChooseOption_WhenCallbackLeavesCurrentPageUnchanged_ReturnsTheOriginalChoiceTask()
    {
        var run = new RunState("event-single-choice", new Overgrowth());
        var ev = (StaticPageTestEvent)ModelDb.Event<StaticPageTestEvent>().MutableClone();
        ev.BeginEvent(run);
        EventOption option = Assert.Single(ev.CurrentOptions);

        Task first = ev.ChooseOption(option);
        await first;
        Task retry = ev.ChooseOption(option);

        Assert.Same(first, retry);
        Assert.Equal(1, ev.ChoiceCount);
    }

    [Fact]
    public async Task ChooseOption_WhenCurrentPageCallbackFaults_AllConcurrentCallersShareTheFault_AndTheOptionCanRetry()
    {
        var run = new RunState("event-fault-retry", new Overgrowth());
        var ev = (FaultThenRetryTestEvent)ModelDb.Event<FaultThenRetryTestEvent>().MutableClone();
        ev.BeginEvent(run);
        EventOption option = Assert.Single(ev.CurrentOptions);

        Task first = ev.ChooseOption(option);
        await ev.FirstChoiceStarted.Task;
        Task concurrent = ev.ChooseOption(option);
        Assert.Same(first, concurrent);

        ev.ReleaseFirstFault.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);
        await Assert.ThrowsAsync<InvalidOperationException>(() => concurrent);

        Task retry = ev.ChooseOption(option);
        Assert.NotSame(first, retry);
        await retry;

        Assert.Equal(2, ev.AttemptCount);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task ChooseOption_WhenCurrentPageCallbackIsCanceled_TheOptionCanRetry()
    {
        var run = new RunState("event-cancel-retry", new Overgrowth());
        var ev = (CancelThenRetryTestEvent)ModelDb.Event<CancelThenRetryTestEvent>().MutableClone();
        ev.BeginEvent(run);
        EventOption option = Assert.Single(ev.CurrentOptions);

        Task first = ev.ChooseOption(option);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);

        await ev.ChooseOption(option);

        Assert.Equal(2, ev.AttemptCount);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task ChooseOption_WhenCallbackTransitionsPageBeforeCompleting_SerializesUntilCallbackReturns()
    {
        var run = new RunState("event-transition-before-completion", new Overgrowth());
        var ev = (TransitionBeforeCompletionTestEvent)ModelDb.Event<TransitionBeforeCompletionTestEvent>().MutableClone();
        ev.BeginEvent(run);

        Task firstChoice = ev.ChooseOption(Assert.Single(ev.CurrentOptions));
        await ev.PageTwoOffered.Task;
        Assert.False(firstChoice.IsCompleted);
        EventOption pageTwoOption = Assert.Single(ev.CurrentOptions);

        Assert.Throws<InvalidOperationException>(() => ev.BeginEvent(run));
        Assert.Throws<InvalidOperationException>(() => { _ = ev.ChooseOption(pageTwoOption); });

        ev.ReleaseFirstChoice.SetResult();
        await firstChoice;

        Assert.Equal(1, ev.PostBarrierMutationCount);
        await ev.ChooseOption(pageTwoOption);
        Assert.Equal(1, ev.PageTwoChoiceCount);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task ChooseOption_WhenFaultedTaskContinuationRetriesImmediately_StartsANewAttempt()
    {
        var run = new RunState("event-immediate-fault-retry", new Overgrowth());
        var ev = (FaultThenRetryTestEvent)ModelDb.Event<FaultThenRetryTestEvent>().MutableClone();
        ev.BeginEvent(run);
        EventOption option = Assert.Single(ev.CurrentOptions);

        Task first = ev.ChooseOption(option);
        await ev.FirstChoiceStarted.Task;
        Task<Task> retryAfterFault = Task.Run(async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => first);
            return ev.ChooseOption(option);
        });

        ev.ReleaseFirstFault.SetResult();
        Task retry = await retryAfterFault;
        await retry;

        Assert.Equal(2, ev.AttemptCount);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public void MutableClone_ResetsPreviousEventRunState()
    {
        var run = new RunState("event-clone-reset", new Overgrowth());
        var ev = (TwoPageTestEvent)ModelDb.Event<TwoPageTestEvent>().MutableClone();
        ev.BeginEvent(run);

        var clone = (TwoPageTestEvent)ev.MutableClone();

        Assert.True(clone.IsMutable);
        Assert.Empty(clone.CurrentOptions);
        Assert.False(clone.IsFinished);
        Assert.Throws<InvalidOperationException>(() => _ = clone.RunState);
        Assert.Throws<InvalidOperationException>(() => _ = clone.Owner);
    }


    private static (RunState Run, Player Player) CreateRunWithPlayer(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = CreatePlayer(run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static Player CreatePlayer(RunState run) =>
        Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
}
