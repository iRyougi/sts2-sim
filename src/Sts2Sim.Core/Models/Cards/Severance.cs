using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Severance</c>：2 费，造成 13（升级 18）伤害，然后生成 3 张 <see cref="Soul"/>：
/// 依次随机放入抽牌堆、弃牌堆、手牌。</summary>
public sealed class Severance : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 2;

    private decimal Damage => IsUpgraded ? 18m : 13m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        List<Soul> souls = Soul.Create(Owner, 3);

        await CardPileCmd.Generate(CombatState!, souls[0], PileType.Draw, Owner, CardPilePosition.Random);
        await CardPileCmd.Generate(CombatState!, souls[1], PileType.Discard, Owner);
        await CardPileCmd.Generate(CombatState!, souls[2], PileType.Hand, Owner);
    }
}
