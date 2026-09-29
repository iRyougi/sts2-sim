using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Enchantments;

public sealed class Goopy : EnchantmentModel
{
    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) && card.Tags.Contains(CardTag.Defend);

    public override void OnAttached(CardModel card) => card.AddKeywordInternal(CardKeyword.Exhaust);

    public override Task OnPlay(CardModel card, CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card, card))
            return Task.CompletedTask;

        AssignMagnitude(Magnitude + 1m);
        Goopy? deckEnchantment = card.DeckVersion?.Enchantments.OfType<Goopy>().SingleOrDefault();
        if (deckEnchantment is not null)
            deckEnchantment.AssignMagnitude(deckEnchantment.Magnitude + 1m);
        return Task.CompletedTask;
    }

    public override decimal EnchantBlockAdditive(decimal amount) => Magnitude - 1m;
}
