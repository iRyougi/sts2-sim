using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class PhantomBlades : CardModel
{
    private decimal _amount = 9m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay cardPlay) => PowerCmd.Apply<PhantomBladesPower>(CombatState!, Owner.Creature, _amount, Owner.Creature, this);
    protected override void OnUpgrade() => _amount += 3m;
}
