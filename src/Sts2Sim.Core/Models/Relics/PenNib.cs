using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PenNib : RelicModel
{
    private int _attacksPlayed;
    private CardPlay? _doubleDamagePlay;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner &&
            cardPlay.Card.Type == CardType.Attack)
        {
            _attacksPlayed++;
            if (_attacksPlayed % 10 == 0)
            {
                _doubleDamagePlay = cardPlay;
            }
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay) =>
        ReferenceEquals(cardPlay, _doubleDamagePlay) &&
        ReferenceEquals(cardSource, cardPlay?.Card) &&
        dealer == Owner.Creature &&
        cardSource?.Owner == Owner &&
        props.IsPoweredAttack()
            ? 2m
            : 1m;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay, _doubleDamagePlay))
        {
            _doubleDamagePlay = null;
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_attacksPlayed);
        context.AssertTransientEmpty(_doubleDamagePlay is null, nameof(_doubleDamagePlay));
    }
}
