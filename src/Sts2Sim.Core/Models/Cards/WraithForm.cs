using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class WraithForm : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<IntangiblePower>(CombatState!, Owner.Creature, IsUpgraded ? 3m : 2m, Owner.Creature, this);
        await PowerCmd.Apply<WraithFormPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
}
