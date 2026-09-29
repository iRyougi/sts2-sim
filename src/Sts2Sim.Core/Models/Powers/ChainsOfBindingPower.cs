namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

public sealed class ChainsOfBindingPower : PowerModel
{
    private int _cardsAfflictedThisTurn;
    private bool _boundCardPlayedThisTurn;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (Owner.CombatState?.CurrentSide != CombatSide.Player ||
            !ReferenceEquals(card.Owner, Owner.Player) ||
            _cardsAfflictedThisTurn >= Amount ||
            card.Affliction is not null)
        {
            return;
        }

        if (await CardCmd.Afflict<Bound>(card, 1m) is not null)
        {
            _cardsAfflictedThisTurn++;
        }
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Player, Owner.Player) && cardPlay.Card.Affliction is Bound)
        {
            _boundCardPlayedThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, bool isAutoPlay) =>
        !ReferenceEquals(card.Owner, Owner.Player) ||
        card.Affliction is not Bound ||
        !_boundCardPlayedThisTurn;

    public override Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || !participants.Contains(Owner))
        {
            return Task.CompletedTask;
        }

        _cardsAfflictedThisTurn = 0;
        _boundCardPlayedThisTurn = false;
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards))
        {
            if (card.Affliction is Bound)
            {
                CardCmd.ClearAffliction(card);
            }
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_cardsAfflictedThisTurn);
        builder.Append(_boundCardPlayedThisTurn);
    }
}
