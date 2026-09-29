using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

internal sealed class PrecariousShearsOrderProbe : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public IReadOnlyList<CardModel>? DeckCardsAtDamage { get; private set; }

    public decimal? ObservedAmount { get; private set; }

    public ValueProp? ObservedProps { get; private set; }

    public override Task BeforeDamageReceived(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (ReferenceEquals(target, Owner.Creature))
        {
            DeckCardsAtDamage = Owner.Deck.Cards.ToArray();
            ObservedAmount = amount;
            ObservedProps = props;
        }

        return Task.CompletedTask;
    }
}
