using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Countdown</c>：1 费能力，施加 6（升级 9）层 <see cref="CountdownPower"/>
/// （每个己方回合开始给随机敌人施加层数的灾厄）。</summary>
public sealed class Countdown : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal CountdownAmount => IsUpgraded ? 9m : 6m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<CountdownPower>(CombatState!, Owner.Creature, CountdownAmount, Owner.Creature, this);
}
