using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>对全体敌人每击 3 点伤害，命中次数为本回合已获得星愿量；升级后每击 4 点。
/// 参照 <c>MegaCrit.Sts2.Core.Models.Cards.Radiate</c>，
/// 偏离 #101：同 <see cref="LunarBlast"/>，用逐回合计数器代替完整战斗历史日志。</summary>
public sealed class Radiate : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 3m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // Native Thrash reads Radiate's DamageVar, not its calculated hit count.
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int hits = (int)Owner.PlayerCombatState!.StarsGainedThisTurn;
        await DamageCmd.Attack(_damage).WithHitCount(hits)
            .FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
    }

    protected override void OnUpgrade() => _damage += 1m;
}
