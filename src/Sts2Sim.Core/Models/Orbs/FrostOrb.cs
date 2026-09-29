using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Orbs;

public sealed class FrostOrb : OrbModel
{
    public override decimal PassiveVal => ModifyOrbValue(2m);

    public override decimal EvokeVal => ModifyOrbValue(5m);

    public override Task BeforeTurnEndOrbTrigger(ICombatState combatState) =>
        TriggerPassive(combatState, null);

    public override async Task Passive(ICombatState combatState, Creature? target)
    {
        if (target is not null)
            throw new InvalidOperationException("Frost orbs cannot target creatures.");
        ActivatePassive();
        await CreatureCmd.GainBlock(combatState, Owner.Creature, PassiveVal,
            ValueProp.Unpowered, cardSource: null, cardPlay: null);
        if (!Owner.Creature.HasPower<HibernatePower>())
            return;
        foreach (Player player in combatState.Players)
            if (!ReferenceEquals(player, Owner))
                await CreatureCmd.GainBlock(combatState, player.Creature, PassiveVal,
                    ValueProp.Unpowered, cardSource: null, cardPlay: null);
    }

    public override async Task<IReadOnlyList<Creature>> Evoke(ICombatState combatState)
    {
        Creature[] ownerTarget = [Owner.Creature];
        ActivateEvoke(ownerTarget);
        await CreatureCmd.GainBlock(combatState, Owner.Creature, EvokeVal,
            ValueProp.Unpowered, cardSource: null, cardPlay: null);
        if (!Owner.Creature.HasPower<HibernatePower>())
            return ownerTarget;
        foreach (Player player in combatState.Players)
            if (!ReferenceEquals(player, Owner))
                await CreatureCmd.GainBlock(combatState, player.Creature, EvokeVal,
                    ValueProp.Unpowered, cardSource: null, cardPlay: null);
        return combatState.Players.Select(player => player.Creature).ToArray();
    }
}
