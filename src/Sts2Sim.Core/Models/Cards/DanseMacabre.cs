using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>DanseMacabre</c>：1 费能力，施加 4（升级 6）层 <see cref="DanseMacabrePower"/>
/// （打出已解析费用不低于 2 的牌前获得层数格挡）。</summary>
public sealed class DanseMacabre : CardModel, ICardChoiceBaseValueProvider
{
    private const int Energy = 2;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal DanseMacabreAmount => IsUpgraded ? 6m : 4m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<DanseMacabrePower>(CombatState!, Owner.Creature, DanseMacabreAmount, Owner.Creature, this);
}
