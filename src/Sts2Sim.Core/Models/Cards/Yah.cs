using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Yah : CardModel, ICardChoiceBaseValueProvider
{
    private int Energy => IsUpgraded ? 2 : 1;

    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<YahPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
}
