using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>打击：造成 13（升级 17）点伤害并向手牌生成 1 张 Sadness（原版 <c>SorrowfulStrike</c>）。</summary>
public sealed class SorrowfulStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];
    private decimal Damage => IsUpgraded ? 17m : 13m;
    private const int Cards = 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: Cards);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        for (int i = 0; i < Cards; i++)
        {
            var sadness = (CardModel)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, sadness, PileType.Hand, Owner);
        }
    }
}
