using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>DevourLife</c>：1 费能力，施加 1（升级 2）层 <see cref="DevourLifePower"/>（打出灵魂后召唤层数）。</summary>
public sealed class DevourLife : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal DevourLifeAmount => IsUpgraded ? 2m : 1m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<DevourLifePower>(CombatState!, Owner.Creature, DevourLifeAmount, Owner.Creature, this);
}
