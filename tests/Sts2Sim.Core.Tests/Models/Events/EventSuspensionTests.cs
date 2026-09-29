using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

sealed class SuspendingTestEvent : EventModel
{
    public void BeginForTest() => BeginEvent(new RunState("event-suspension", new Overgrowth()));

    public void SuspendForTest()
    {
        RequestForcedCombat(() => throw new InvalidOperationException("test factory must not run"));
        SuspendForForcedCombat();
    }

    public void FinishThenSuspendForTest()
    {
        RequestForcedCombat(() => throw new InvalidOperationException("test factory must not run"));
        Finish();
        SuspendForForcedCombat();
    }

    public EventOption FirstOptionForTest() => CurrentOptions.Single();

    public ForcedCombatOutcome? SeenOutcome { get; private set; }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        new[] { new EventOption("WAIT", () => Task.CompletedTask) };

    protected override void AfterForcedCombat(ForcedCombatOutcome outcome)
    {
        SeenOutcome = outcome;
    }
}

[Collection("ModelDb")]
public sealed class EventSuspensionTests : IDisposable
{
    public EventSuspensionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(SuspendingTestEvent) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void SuspendForForcedCombat_IsNotFinished()
    {
        var evt = CreateEvent();
        evt.BeginForTest();

        evt.SuspendForTest();

        Assert.True(evt.IsAwaitingForcedCombat);
        Assert.False(evt.IsFinished);
        Assert.Empty(evt.CurrentOptions);
    }

    [Fact]
    public void SuspendForForcedCombat_ClearsFinishedState()
    {
        var evt = CreateEvent();
        evt.BeginForTest();

        evt.FinishThenSuspendForTest();

        Assert.True(evt.IsAwaitingForcedCombat);
        Assert.False(evt.IsFinished);
    }

    [Fact]
    public void ResumeAfterForcedCombat_ClearsSuspensionAndDeliversOutcome()
    {
        var evt = CreateEvent();
        evt.BeginForTest();
        evt.SuspendForTest();

        evt.ResumeAfterForcedCombat(new ForcedCombatOutcome(Victory: true, TimedOut: false));

        Assert.False(evt.IsAwaitingForcedCombat);
        Assert.Equal(new ForcedCombatOutcome(Victory: true, TimedOut: false), evt.SeenOutcome);
    }

    [Fact]
    public void ResumeAfterForcedCombat_WithoutSuspension_Throws()
    {
        var evt = CreateEvent();
        evt.BeginForTest();

        Assert.Throws<InvalidOperationException>(
            () => evt.ResumeAfterForcedCombat(new ForcedCombatOutcome(Victory: true, TimedOut: false)));
    }

    [Fact]
    public async Task ChooseOption_WhileSuspended_Throws()
    {
        var evt = CreateEvent();
        evt.BeginForTest();
        EventOption option = evt.FirstOptionForTest();
        evt.SuspendForTest();

        await Assert.ThrowsAsync<InvalidOperationException>(() => evt.ChooseOption(option));
    }

    private static SuspendingTestEvent CreateEvent() =>
        (SuspendingTestEvent)ModelDb.Event<SuspendingTestEvent>().MutableClone();
}
