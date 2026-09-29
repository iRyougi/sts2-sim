using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Orbs;

public sealed class DarkOrb : OrbModel
{
    private decimal _evokeVal = 6m;

    public override decimal PassiveVal => ModifyOrbValue(6m);

    public override decimal EvokeVal => _evokeVal;

    internal decimal RawEvokeValue => _evokeVal;

    public override Task BeforeTurnEndOrbTrigger(ICombatState combatState) =>
        TriggerPassive(combatState, null);

    public override Task Passive(ICombatState combatState, Creature? target)
    {
        if (target is not null)
            throw new InvalidOperationException("Dark orbs cannot target creatures.");
        ActivatePassive();
        _evokeVal += PassiveVal;
        return Task.CompletedTask;
    }

    public override async Task<IReadOnlyList<Creature>> Evoke(ICombatState combatState)
    {
        IReadOnlyList<Creature> enemies = combatState.HittableEnemies;
        if (enemies.Count == 0)
            return [];
        Creature weakest = enemies.MinBy(enemy => enemy.CurrentHp)!;
        Creature[] targets = [weakest];
        ActivateEvoke(targets);
        await CreatureCmd.Damage(combatState, targets, EvokeVal, ValueProp.Unpowered,
            Owner.Creature, cardSource: null, cardPlay: null);
        return targets;
    }
}
