using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SerratedArrows : CardModel, ICardChoiceBaseValueProvider
{
    private decimal PowerAmount => IsUpgraded ? 50m : 25m;

    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<SerratedArrowsPower>(CombatState!, Owner.Creature, PowerAmount, Owner.Creature, this);
}
