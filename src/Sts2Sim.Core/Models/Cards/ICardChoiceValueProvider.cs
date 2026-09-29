namespace Sts2Sim.Core.Models.Cards;

/// <summary>Legacy aggregate used by combat-search evaluation and snapshots.
/// Card-choice branching requires <see cref="ICardChoiceBaseValueProvider"/> instead: an aggregate
/// cannot establish the upstream literal keys or their Energy/Stars values.</summary>
public interface ICardChoiceValueProvider
{
    double CardChoiceValue { get; }
}
