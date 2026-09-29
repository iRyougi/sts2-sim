using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Keep three bonus Strength while the owner's HP is at most half in combat.</summary>
public sealed class RedSkull : RelicModel
{
    private bool _strengthApplied;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterRoomEntered(AbstractRoom room) => room is CombatRoom
        ? ReconcileStrength()
        : Task.CompletedTask;

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        _ = creature;
        _ = delta;
        return Owner.Creature.CombatState is { } combatState && combatState.IsLiveCombat()
            ? ReconcileStrength()
            : Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _strengthApplied = false;
        return Task.CompletedTask;
    }

    private async Task ReconcileStrength()
    {
        Creature creature = Owner.Creature;
        if (creature.CombatState is not { } combatState)
            return;
        bool aboveHalf = creature.CurrentHp > creature.MaxHp * 0.5m;
        if (aboveHalf && _strengthApplied)
        {
            await PowerCmd.Apply<StrengthPower>(combatState, creature, -3m, creature, null);
            _strengthApplied = false;
        }
        else if (!aboveHalf && !_strengthApplied)
        {
            await PowerCmd.Apply<StrengthPower>(combatState, creature, 3m, creature, null);
            _strengthApplied = true;
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_strengthApplied);
    }
}
