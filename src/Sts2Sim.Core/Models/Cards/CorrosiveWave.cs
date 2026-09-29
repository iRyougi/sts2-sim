using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CorrosiveWave : CardModel
{
    private decimal Amount => IsUpgraded ? 3m : 2m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay play) => PowerCmd.Apply<CorrosiveWavePower>(CombatState!, Owner.Creature, Amount, Owner.Creature, this);

}
