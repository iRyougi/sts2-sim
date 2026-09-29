namespace Sts2Sim.Core.Entities.Cards;

/// <summary>Tracks the resources spent by this play and its resolved energy/star values.
/// X effects use the separate captured X values, which can differ after prepayment.</summary>
public readonly record struct ResourceInfo(int EnergySpent, int EnergyValue, int StarsSpent, int StarValue)
{
    // Prepaid autoplay keeps the X amount captured while spending in hand. The public
    // Value fields still describe the resources observed when CardPlay starts.
    internal int? CapturedEnergyXValue { get; init; }

    internal int? CapturedStarXValue { get; init; }

    internal int EnergyXValue => CapturedEnergyXValue ?? EnergyValue;

    internal int StarXValue => CapturedStarXValue ?? StarValue;
}

internal readonly record struct PrepaidXCapture(int? Energy, int? Stars);
