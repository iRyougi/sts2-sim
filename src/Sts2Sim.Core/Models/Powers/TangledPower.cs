namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

/// <summary>
/// Afflicts the owner's attack cards with <see cref="Entangled"/> and raises their combat energy cost until its
/// side turn ends. <see cref="CardCmd.Afflict{T}"/> refuses cards that already carry another affliction, so those
/// attacks keep their cost, as in the native power.
/// </summary>
public sealed class TangledPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards)
                     .Where(card => card.Type == CardType.Attack).ToArray())
        {
            await CardCmd.Afflict<Entangled>(card, 1m);
        }
    }

    public override async Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner == Owner.Player && card.Affliction is null && card.Type == CardType.Attack)
        {
            await CardCmd.Afflict<Entangled>(card, 1m);
        }
    }

    /// <summary>Entry rollback (simulator-only): a card whose combat entry failed must not keep the Entangled
    /// this listener attached during that entry.</summary>
    public override Task AfterCardEntryAborted(CardModel card)
    {
        if (card.Affliction is Entangled)
        {
            CardCmd.ClearAffliction(card);
        }

        return Task.CompletedTask;
    }

    /// <remarks>X-cost cards never reach here natively: <c>CardEnergyCost.GetWithModifiers</c> returns before the
    /// combat hooks. The <see cref="CardModel.CostsXEnergy"/> guard keeps that for direct hook callers.</remarks>
    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        if (card.Owner != Owner.Player || card.CostsXEnergy || card.Affliction is not Entangled)
        {
            modifiedCost = originalCost;
            return false;
        }

        modifiedCost = originalCost + Amount;
        return true;
    }

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        IEnumerable<CardModel> cards = oldOwner.Player?.PlayerCombatState?.AllPiles.SelectMany(pile => pile.Cards)
            ?? Array.Empty<CardModel>();
        foreach (CardModel card in cards)
        {
            if (card.Affliction is Entangled)
            {
                CardCmd.ClearAffliction(card);
            }
        }

        return Task.CompletedTask;
    }
}
