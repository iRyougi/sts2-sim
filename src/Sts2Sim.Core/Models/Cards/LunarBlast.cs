using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>每击 4 点伤害，命中次数为本回合已打出的技能牌数；升级后每击 5 点。
/// 参照 <c>MegaCrit.Sts2.Core.Models.Cards.LunarBlast</c>，
/// 偏离 #101：真实源码查战斗历史日志统计"本回合已完成的技能牌出牌次数"，本项目没有完整历史日志系统，
/// 改用 <see cref="Entities.Players.PlayerCombatState.SkillCardsPlayedThisTurn"/> 逐回合计数器
/// （该计数器在 <c>CardModel.PlayAsync</c> 里于 Hook.AfterCardPlayed 之后递增，所以不包含 LunarBlast 自己这次出牌，
/// 与真实源码"历史记录里只有已完成的出牌"语义一致）。</summary>
public sealed class LunarBlast : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 4m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // CalculatedHits changes hit count; native DamageVar remains per-hit damage.
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = Owner.PlayerCombatState!.SkillCardsPlayedThisTurn;
        await DamageCmd.Attack(_damage).WithHitCount(hits)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _damage += 1m;
}
