using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class FiendFire : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    private decimal _damage = 7m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        CardModel[] cards = Owner.PlayerCombatState!.Hand.Cards.ToArray();
        foreach (CardModel card in cards)
        {
            await CardPileCmd.Exhaust(CombatState!, card);
        }
        await DamageCmd.Attack(_damage).WithHitCount(cards.Length)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _damage += 3m;
}
