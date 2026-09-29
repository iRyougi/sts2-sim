using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Anticipate : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<AnticipatePower>(CombatState!, Owner.Creature, IsUpgraded ? 4m : 2m, Owner.Creature, this);
}
