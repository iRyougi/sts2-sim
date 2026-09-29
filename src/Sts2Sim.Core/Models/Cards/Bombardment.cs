using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,18伤害;耗尽后自动回放一次。逐字移植核心行为（<c>MegaCrit.Sts2.Core.Models.Cards.Bombardment</c>），
/// 偏离 #103：真实源码用"自动出牌阶段"hook，在这张卡真正落进耗尽堆之后再触发一次新的出牌流程；本项目没有
/// 这套阶段划分,直接在同一次 OnPlay 内用一个"是否已回放过"标记再结算一次伤害,效果上等价于"打一次顶两次伤害"，
/// 但不会像真实游戏那样产生第二个独立的 CardPlay/出牌历史记录。</summary>
public sealed class Bombardment : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 18m;
    private bool _hasReplayed;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 3;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (!_hasReplayed)
        {
            _hasReplayed = true;
            await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        }
    }

    protected override void OnUpgrade() => _damage += 6m;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_hasReplayed);
    }
}
