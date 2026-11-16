using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Nock : CardModel, ICardChoiceBaseValueProvider
{
    private decimal Aim => IsUpgraded ? 7m : 5m;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<AimPower>(CombatState!, Owner.Creature, Aim, Owner.Creature, this);
}
