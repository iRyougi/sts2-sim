using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>BansheesCry</c>：9 费（升级 7）攻击，对所有敌人造成 33。本场战斗中持有者每打出完一张虚无牌，
/// 这张牌的费用减 2：进入战斗时按历史里已完成的虚无打出一次性补扣（战斗中生成的克隆不补扣），之后在
/// <see cref="AfterCardPlayed"/> 里逐张扣。</summary>
public sealed class BansheesCry : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const decimal Damage = 33m;
    private const int EnergyReductionPerEtherealPlay = 2;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 9;

    public CardChoiceBaseValues? CardChoiceBaseValues =>
        new(Damage: (double)Damage, Energy: EnergyReductionPerEtherealPlay);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override Task OnPlay(CardPlay cardPlay) =>
        DamageCmd.Attack(Damage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();

    protected override void OnUpgrade() => ReduceEnergyCost(2);

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (ReferenceEquals(card, this) && CloneOf is null && CombatState is CombatState concrete)
        {
            int etherealPlays = concrete.SemanticHistory.CountThisCombat(
                concrete, CombatSemanticHistory.ActorEvent.EtherealPlayFinished, Owner);
            AddEnergyCostThisCombat(-etherealPlays * EnergyReductionPerEtherealPlay);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner, Owner) && cardPlay.Card.Keywords.Contains(CardKeyword.Ethereal))
            AddEnergyCostThisCombat(-EnergyReductionPerEtherealPlay);
        return Task.CompletedTask;
    }
}
