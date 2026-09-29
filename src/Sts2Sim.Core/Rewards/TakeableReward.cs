namespace Sts2Sim.Core.Rewards;

/// <summary>Base class for rewards resolved by taking them once.</summary>
public abstract class TakeableReward : Reward
{
    private readonly object _takeLock = new();
    private Task? _takeTask;

    protected TakeableReward(Entities.Players.Player player) : base(player)
    {
    }

    public Task Take()
    {
        lock (_takeLock)
        {
            if (_takeTask is null && !CanTake) return Task.CompletedTask;
            return _takeTask ??= TakeOnce();
        }
    }

    public virtual bool CanTake => true;

    public Task Skip()
    {
        lock (_takeLock)
        {
            if (_takeTask is not null) return _takeTask;
            IsResolved = true;
            return _takeTask = Task.CompletedTask;
        }
    }

    private async Task TakeOnce()
    {
        try
        {
            await OnTake();
        }
        finally
        {
            IsResolved = true;
        }
    }

    protected abstract Task OnTake();
}
