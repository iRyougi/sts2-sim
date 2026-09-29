namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Exposes the source card's live damage variable for Thrash. Implementations follow the
/// native lookup order: CalculatedDamage, then Damage, then OstyDamage; false means none.
/// </summary>
public interface ICardDamageVariableProvider
{
    bool TryGetThrashDamageVariable(out decimal amount);
}
