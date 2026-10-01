using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// Afflicts the owner's unafflicted combat cards with <see cref="Ringing"/> and permits only the first card play
/// each turn while a Ringing card is involved. As in the native power, cards that already carry another
/// affliction are left alone, and removal clears only Ringing.
/// </summary>
public sealed class RingingPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToArray())
        {
            if (card.Affliction is null)
            {
                await CardCmd.Afflict<Ringing>(card, 1m);
            }
        }
    }

    public override async Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner == Owner.Player && card.Affliction is null)
        {
            await CardCmd.Afflict<Ringing>(card, 1m);
        }
    }

    /// <summary>Entry rollback (simulator-only): a card whose combat entry failed must not keep the Ringing
    /// this listener attached during that entry.</summary>
    public override Task AfterCardEntryAborted(CardModel card)
    {
        if (card.Affliction is Ringing)
        {
            CardCmd.ClearAffliction(card);
        }

        return Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, bool isAutoPlay)
    {
        if (card.Owner.Creature != Owner || card.Affliction is not Ringing)
        {
            return true;
        }

        return card.Owner.PlayerCombatState!.CardPlaysStartedThisTurn == 0;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
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
            if (card.Affliction is Ringing)
            {
                CardCmd.ClearAffliction(card);
            }
        }

        return Task.CompletedTask;
    }
}
