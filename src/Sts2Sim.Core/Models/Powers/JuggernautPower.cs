using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class JuggernautPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterBlockGained(
        Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (amount <= 0m || creature != Owner)
        {
            return Task.CompletedTask;
        }
        IReadOnlyList<Creature> enemies = Owner.CombatState!.HittableEnemies;
        if (enemies.Count == 0)
        {
            return Task.CompletedTask;
        }
        Creature target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(enemies)!;
        return CreatureCmd.Damage(Owner.CombatState, [target], Amount,
            ValueProp.Unpowered, Owner, null, null);
    }
}
