using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Enlightenment : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        // 游戏 Enlightenment：对整手牌 SetThisCombat / SetThisTurnOrUntilPlayed(1, reduceOnly: true)，不筛费用；
        // reduceOnly 保证它不会把已被压到 0 的牌抬回 1，而低修饰过期后它才生效。
        foreach (CardModel card in Owner.PlayerCombatState!.Hand.Cards.ToList())
        {
            if (IsUpgraded) card.SetTemporaryCostOverrideThisCombat(1, reduceOnly: true);
            else card.SetTemporaryCostOverrideThisTurnOrUntilPlayed(1, reduceOnly: true);
        }
        return Task.CompletedTask;
    }
}
