using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>At the owner's side turn start, give its owner 2 Strength and living opponents 1.</summary>
public sealed class Brimstone : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        _ = side;
        Creature owner = Owner.Creature;
        if (!participants.Contains(owner))
            return;

        ICombatState combatState = owner.CombatState!;
        await PowerCmd.Apply<StrengthPower>(combatState, owner, 2m, owner, null);
        foreach (Creature opponent in combatState.GetOpponentsOf(owner).Where(creature => creature.IsAlive))
            await PowerCmd.Apply<StrengthPower>(combatState, opponent, 1m, null, null);
    }
}
