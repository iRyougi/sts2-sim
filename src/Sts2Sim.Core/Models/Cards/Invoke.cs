using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Invoke</c>：下回合召唤 2（升级 3），下回合获得 2（升级 3）能量。
/// 原版 <c>CardEnergyCost.SetThisTurn</c> 的文档注释说它"Osty 本回合攻击过则费用为 0"，但 v0.111.0 的
/// Invoke 本身没有任何费用逻辑（那是 <see cref="Flatten"/> 的行为），这里按实际代码实现。</summary>
public sealed class Invoke : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private int Summon => IsUpgraded ? 3 : 2;

    private int Energy => IsUpgraded ? 3 : 2;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<SummonNextTurnPower>(CombatState!, Owner.Creature, Summon, Owner.Creature, this);
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
    }
}
