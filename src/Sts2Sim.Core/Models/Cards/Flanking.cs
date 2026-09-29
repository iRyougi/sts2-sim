using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Flanking : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 2;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        return PowerCmd.Apply<FlankingPower>(CombatState!, cardPlay.Target, 2m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
