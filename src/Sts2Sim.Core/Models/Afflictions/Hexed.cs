namespace Sts2Sim.Core.Models.Afflictions;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

/// <summary>Hex's combat-only card attachment, which makes its card Ethereal while Hex remains active.</summary>
public sealed class Hexed : AfflictionModel
{
    public override bool TryModifyKeywords(ISet<CardKeyword> keywords) =>
        keywords.Add(CardKeyword.Ethereal);

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (ReferenceEquals(card, Card) && !card.Owner.Creature.HasPower<HexPower>())
        {
            CardCmd.ClearAffliction(card);
        }

        return Task.CompletedTask;
    }
}
