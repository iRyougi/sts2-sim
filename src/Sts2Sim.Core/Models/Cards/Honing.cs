using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>箭矢攻击伤害按百分比乘算提高（原版 <c>Honing</c>）。</summary>
public sealed class Honing : CardModel, ICardChoiceBaseValueProvider
{
    private const decimal PowerAmount = 100m;

    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<HoningPower>(CombatState!, Owner.Creature, PowerAmount, Owner.Creature, this);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
