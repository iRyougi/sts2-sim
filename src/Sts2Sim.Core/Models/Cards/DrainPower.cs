using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>DrainPower</c>：1 费攻击，造成 10（升级 12），再从弃牌堆随机升级 2（升级 3）张可升级的牌。
/// 原版 <c>TakeRandom</c> 先把候选物化再用 <c>CombatCardSelection</c> 整表 UnstableShuffle 后取前 N 张，
/// 洗牌在升级之前发生，即使战斗随即结束也照样消耗随机数。</summary>
public sealed class DrainPower : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal Damage => IsUpgraded ? 12m : 10m;

    private int Cards => IsUpgraded ? 3 : 2;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: Cards);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        List<CardModel> selected = Owner.PlayerCombatState!.DiscardPile.Cards
            .Where(card => card.IsUpgradable).ToList()
            .UnstableShuffle(Owner.RunState.Rng.CombatCardSelection)
            .Take(Cards)
            .ToList();
        foreach (CardModel card in selected)
        {
            // 原版 CardCmd.Upgrade 在战斗结束中（例如这次攻击打死了最后一个敌人）不升级。
            if (!CombatState!.IsOverOrEnding())
                CardCmd.Upgrade(card);
        }
    }
}
