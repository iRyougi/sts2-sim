using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成伤害；若仍有可用箭矢，从弃牌堆选一张 Shoot 牌入手（原版 <c>ArrowStab</c>）。</summary>
public sealed class ArrowStab : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    private decimal Damage => IsUpgraded ? 12m : 9m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (ArrowCmd.PlayerHasAnyArrows(Owner) &&
            Owner.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            CardModel? selected = (await CardSelectCmd.FromCombatPile(
                CombatState!,
                Owner,
                Owner.PlayerCombatState.DiscardPile,
                1,
                1,
                this,
                c => c.Tags.Contains(CardTag.Shoot),
                cancelable: false)).FirstOrDefault();
            if (selected is not null)
            {
                CardPileCmd.Add(selected, PileType.Hand);
            }
        }
    }
}
