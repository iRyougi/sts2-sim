using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>10 格挡；若抽牌堆顶是本卡则自动出牌。偏离 #103：原版在
/// <c>AfterAutoPostPlayPhaseEntered</c> 检查；模拟器仍在每张牌打出后的
/// <c>AfterCardPlayed</c> 检查。09-C1 已接入 AutoPostPlay 阶段，旧注释所说的“没有阶段划分”
/// 不再成立；本卡触发时机与原版的差异仍需独立夹具验证。</summary>
public sealed class IAmInvincible : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 10m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner &&
            cardPlay.Card != this &&
            Owner.PlayerCombatState!.DrawPile.Cards.FirstOrDefault() == this)
        {
            await PlayAsync(target: null);
        }
    }

    protected override void OnUpgrade() => _block += 3m;
}
