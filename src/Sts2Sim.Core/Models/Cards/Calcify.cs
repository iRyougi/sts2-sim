using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Calcify</c>：1 费能力，施加 4（升级 6）层 <see cref="CalcifyPower"/>（自己的 Osty 有力攻击伤害 +层数）。</summary>
public sealed class Calcify : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal CalcifyAmount => IsUpgraded ? 6m : 4m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<CalcifyPower>(CombatState!, Owner.Creature, CalcifyAmount, Owner.Creature, this);
}
