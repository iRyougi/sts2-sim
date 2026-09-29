using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BouncingFlask : CardModel
{
    private int Repeats => IsUpgraded ? 4 : 3;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.RandomEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay play)
    {
        for (int i = 0; i < Repeats; i++)
        {
            Creature? enemy = Owner.RunState.Rng.CombatTargets.NextItem(CombatState!.HittableEnemies);
            if (enemy is not null) await PowerCmd.Apply<PoisonPower>(CombatState, enemy, 3m, Owner.Creature, this);
        }
    }

}
