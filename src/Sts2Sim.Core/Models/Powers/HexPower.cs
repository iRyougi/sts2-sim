namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

/// <summary>Afflicts the owner's current and future combat cards with <see cref="Hexed"/> and makes them Ethereal
/// while it lasts.</summary>
public sealed class HexPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    /// <summary>A global keyword (<see cref="KeywordSources.Global"/>), as in the native power: it disappears with
    /// the power and is never stored in the card's local keywords.</summary>
    public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords) =>
        card.Owner == Owner.Player && card.Affliction is Hexed && keywords.Add(CardKeyword.Ethereal);

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (CardModel card in CombatCards())
        {
            await Afflict(card);
        }
    }

    public override Task AfterCardEnteredCombat(CardModel card) => Afflict(card);

    public override async Task AfterDeath(Creature target)
    {
        if (ReferenceEquals(target, Applier))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach (CardModel card in CombatCards())
        {
            if (card.Affliction is Hexed)
            {
                CardCmd.ClearAffliction(card);
            }
        }

        return Task.CompletedTask;
    }

    private IEnumerable<CardModel> CombatCards() =>
        Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards);

    private Task Afflict(CardModel card) =>
        card.Owner == Owner.Player && card.Affliction is null
            ? CardCmd.Afflict<Hexed>(card, Amount)
            : Task.CompletedTask;
}
