using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MeteorShower : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 2, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies,
        false, false, false, Array.Empty<CardKeyword>(),
        14m, 1, 0m, 0, 0, 0,
        2m, 2m, 0m, 0m, 0m,
        7m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal damage = Spec.Damage + (IsUpgraded ? Spec.UpgradeDamage : 0m);
        await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute();

        Creature[] targets = CombatState!.HittableEnemies.ToArray();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(
                CombatState,
                target,
                Spec.Weak,
                Owner.Creature,
                this);
            await PowerCmd.Apply<VulnerablePower>(
                CombatState,
                target,
                Spec.Vulnerable,
                Owner.Creature,
                this);
        }
    }
}
