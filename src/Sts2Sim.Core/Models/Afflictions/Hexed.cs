namespace Sts2Sim.Core.Models.Afflictions;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

/// <summary>Hex's combat-only card attachment. The Ethereal it implies is a global keyword granted by
/// <see cref="HexPower.TryModifyKeywordsInCombat"/>, never a keyword of the card itself.</summary>
public sealed class Hexed : AfflictionModel
{
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (ReferenceEquals(card, Card) && !card.Owner.Creature.HasPower<HexPower>())
        {
            CardCmd.ClearAffliction(card);
        }

        return Task.CompletedTask;
    }
}
