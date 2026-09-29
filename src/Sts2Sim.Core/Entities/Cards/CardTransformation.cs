using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Entities.Cards;

public readonly struct CardTransformation
{
    public CardModel Original { get; }
    public CardModel? Replacement { get; }
    public IEnumerable<CardModel>? ReplacementOptions { get; }
    public bool IsInCombat { get; }

    public CardTransformation(CardModel original)
    {
        if (!original.IsTransformable) throw new InvalidOperationException("Non-removable cards cannot be transformed.");
        Original = original;
        IsInCombat = original.CombatState is not null;
    }

    public CardTransformation(CardModel original, CardModel replacement) : this(original) => Replacement = replacement;
    public CardTransformation(CardModel original, IEnumerable<CardModel> options) : this(original) => ReplacementOptions = options;

    public CardModel? GetReplacement(Rng? rng)
    {
        if (Replacement is not null) return Replacement;
        if (rng is null) throw new ArgumentException("RNG must be passed for random transformation.", nameof(rng));
        if (!Original.IsTransformable) return null;
        return ReplacementOptions is null
            ? CardFactory.CreateRandomCardForTransform(Original, IsInCombat, rng)
            : CardFactory.CreateRandomCardForTransform(Original, ReplacementOptions, IsInCombat, rng);
    }
}
