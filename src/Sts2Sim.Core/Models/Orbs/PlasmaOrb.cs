using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Orbs;

public sealed class PlasmaOrb : OrbModel
{
    public override decimal PassiveVal => 1m;

    public override decimal EvokeVal => 2m;

    public override Task AfterTurnStartOrbTrigger(ICombatState combatState) =>
        TriggerPassive(combatState, null);

    public override async Task Passive(ICombatState combatState, Creature? target)
    {
        if (target is not null)
            throw new InvalidOperationException("Plasma orbs cannot target creatures.");
        ActivatePassive();
        await PlayerCmd.GainEnergy(PassiveVal, Owner);
    }

    public override async Task<IReadOnlyList<Creature>> Evoke(ICombatState combatState)
    {
        Creature[] targets = [Owner.Creature];
        ActivateEvoke(targets);
        await PlayerCmd.GainEnergy(EvokeVal, Owner);
        return targets;
    }
}
