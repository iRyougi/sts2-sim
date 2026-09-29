using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Shadowmeld : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<ShadowmeldPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
