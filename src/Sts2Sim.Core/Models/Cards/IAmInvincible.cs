using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>10 格挡；自动出牌后阶段（<c>AfterAutoPostPlayPhaseEntered</c>）若抽牌堆顶是本卡，
/// 用 <c>AutoPlayCmd.FromTopOfDrawPile</c> 从抽牌堆自动打出（不付费用）。逐字移植
/// <c>MegaCrit.Sts2.Core.Models.Cards.IAmInvincible</c>。</summary>
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

    public override async Task AfterAutoPostPlayPhaseEntered(Player player)
    {
        if (player == Owner &&
            Owner.PlayerCombatState!.DrawPile.Cards.FirstOrDefault() == this)
        {
            await AutoPlayCmd.FromTopOfDrawPile(CombatState!, Owner, 1, forceExhaust: false);
        }
    }

    protected override void OnUpgrade() => _block += 3m;
}
