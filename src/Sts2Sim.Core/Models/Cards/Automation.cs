using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Automation : CardModel
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override void OnUpgrade() => ReduceEnergyCost(1);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<AutomationPower>(
            CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
}
