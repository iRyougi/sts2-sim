namespace Sts2Sim.Core.Entities.Cards;

/// <summary>Tracks the resources spent by a card play and the resolved energy/Stars values used by its effects.</summary>
public readonly record struct ResourceInfo(int EnergySpent, int EnergyValue, int StarsSpent, int StarValue);
