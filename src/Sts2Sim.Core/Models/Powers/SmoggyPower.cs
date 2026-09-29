namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

public sealed class SmoggyPower : PowerModel
{
    private bool _skillCardPlayStartedThisTurn;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Type == CardType.Skill)
        {
            _skillCardPlayStartedThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner != Owner.Player || card.Type != CardType.Skill ||
            Owner.Player.PlayerCombatState is not { } combat || !_skillCardPlayStartedThisTurn ||
            card.Affliction is not null)
        {
            return;
        }

        await CardCmd.Afflict<Smog>(card, 1m);
    }
    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner?.Creature != Owner || cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards))
        {
            if (card.Type == CardType.Skill && card.Affliction is null)
            {
                await CardCmd.Afflict<Smog>(card, 1m);
            }
        }
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.Player?.PlayerCombatState is not { } combat)
        {
            return Task.CompletedTask;
        }

        _skillCardPlayStartedThisTurn = false;

        foreach (CardModel card in combat.AllPiles.SelectMany(pile => pile.Cards)
                     .Where(card => card.Affliction is Smog))
        {
            CardCmd.ClearAffliction(card);
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_skillCardPlayStartedThisTurn);

    public override bool ShouldPlay(CardModel card, bool isAutoPlay) =>
        !ReferenceEquals(card.Owner, Owner.Player) || card.Affliction is not Smog;
}
