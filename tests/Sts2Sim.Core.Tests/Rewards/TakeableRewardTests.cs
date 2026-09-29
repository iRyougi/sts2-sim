using System.Threading;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

public class TakeableRewardTests
{
    private sealed class ControlledReward : TakeableReward
    {
        private readonly bool _failAfterSideEffect;

        public ControlledReward(bool failAfterSideEffect) : base(null!)
        {
            _failAfterSideEffect = failAfterSideEffect;
        }

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int TakeCount;

        public override void Populate(IRunState runState)
        {
        }

        protected override async Task OnTake()
        {
            Interlocked.Increment(ref TakeCount);
            Started.SetResult();
            await Continue.Task;
            if (_failAfterSideEffect)
            {
                throw new InvalidOperationException("Simulated failure after payout.");
            }
        }
    }

    [Fact]
    public async Task Take_ConcurrentCalls_ShareOnePayoutAttempt()
    {
        var reward = new ControlledReward(failAfterSideEffect: false);

        Task first = reward.Take();
        await reward.Started.Task;
        Task retry = reward.Take();
        reward.Continue.SetResult();

        await Task.WhenAll(first, retry);

        Assert.Same(first, retry);
        Assert.Equal(1, reward.TakeCount);
        Assert.True(reward.IsResolved);
    }

    [Fact]
    public async Task Take_WhenPayoutFailsAfterSideEffect_RetryReturnsSameFailureWithoutReplaying()
    {
        var reward = new ControlledReward(failAfterSideEffect: true);

        Task first = reward.Take();
        await reward.Started.Task;
        reward.Continue.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => first);

        Task retry = reward.Take();

        Assert.Same(first, retry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => retry);
        Assert.Equal(1, reward.TakeCount);
        Assert.True(reward.IsResolved);
    }
}
