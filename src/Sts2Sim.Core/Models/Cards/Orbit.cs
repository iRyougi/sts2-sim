using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Orbit : CardModel
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    protected override void OnUpgrade() => ReduceEnergyCost(1);

    protected override Task OnPlay(CardPlay cardPlay)
    {
        return PowerCmd.Apply<OrbitPower>(
            CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
}
