using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ShadowStep : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardCmd.Discard(Owner.PlayerCombatState!.Hand.Cards.ToArray());
        await PowerCmd.Apply<ShadowStepPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
