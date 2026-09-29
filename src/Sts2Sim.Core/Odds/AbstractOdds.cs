using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Odds;

public abstract class AbstractOdds
{
    protected readonly Rng _rng;

    public float CurrentValue { get; protected set; }

    protected AbstractOdds(float initialValue, Rng rng)
    {
        CurrentValue = initialValue;
        _rng = rng;
    }

    public void OverrideCurrentValue(float newValue)
    {
        CurrentValue = newValue;
    }
}
