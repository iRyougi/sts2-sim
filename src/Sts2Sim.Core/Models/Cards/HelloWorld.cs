using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class HelloWorld : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<HelloWorldPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
}
