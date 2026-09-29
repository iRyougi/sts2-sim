using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>27伤害;被抽到手牌时本战斗费用-1。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.KinglyKick</c>）：
/// 真实源码通过战斗内临时费用修饰符减少费用（与升级降费分开计算）；
/// 本项目 <see cref="ReduceEnergyCost"/> 只有一个统一的费用扣减累加器，两种降费叠加即可，效果等价。</summary>
public sealed class KinglyKick : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 27m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 4;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    public override Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (card == this)
        {
            ReduceEnergyCost(1);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => _damage += 8m;
}
