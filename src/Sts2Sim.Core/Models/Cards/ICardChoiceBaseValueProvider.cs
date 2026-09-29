namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Exact current BaseValues of the upstream literal DynamicVar keys, not estimated card effects.
/// Missing keys in a declared set are zero. Aliases and CalculatedDamage/CalculatedBlock do not
/// contribute. Null permits fallback only for types in the frozen source-audited compatibility
/// snapshot; all other types must fail rather than treating an undeclared set as empty.
/// Static GeneratedCardSpec declarations are only for values unchanged during combat except
/// upgrades. Dynamic values require a live getter (override GeneratedCardModel's virtual property).
/// </summary>
public interface ICardChoiceBaseValueProvider
{
    CardChoiceBaseValues? CardChoiceBaseValues { get; }
}

/// <summary>Source literal keys used by CombatSolver. All other keys are deliberately excluded.</summary>
public readonly record struct CardChoiceBaseValues(
    double Damage = 0, double Block = 0, double Cards = 0, double Energy = 0, double Stars = 0)
{
    public double GetBaseValue(string key) => key switch
    {
        "Damage" => Damage,
        "Block" => Block,
        "Cards" => Cards,
        "Energy" => Energy,
        "Stars" => Stars,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not a CombatSolver literal key.")
    };

    public CardChoiceBaseValues WithUpgrade(CardChoiceBaseValues delta, int level) => new(
        Damage + delta.Damage * level, Block + delta.Block * level,
        Cards + delta.Cards * level, Energy + delta.Energy * level, Stars + delta.Stars * level);
}
