using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class StoneCalendar : RelicModel
{
    private bool _shouldTriggerThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        _shouldTriggerThisTurn = participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState is { TurnNumber: 7 };
        if (_shouldTriggerThisTurn)
        {
            Status = RelicStatus.Active;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        // Source sets Normal for every owner side-turn end, before the DamageTurn check.
        if (participants.Contains(Owner.Creature))
        {
            Status = RelicStatus.Normal;
        }

        if (!_shouldTriggerThisTurn || !participants.Contains(Owner.Creature))
        {
            return;
        }

        _shouldTriggerThisTurn = false;
        ICombatState combatState = Owner.Creature.CombatState!;
        await CreatureCmd.Damage(
            combatState,
            combatState.HittableEnemies.ToArray(),
            52m,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }

    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        _shouldTriggerThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            _shouldTriggerThisTurn = false;
            Status = RelicStatus.Normal;
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_shouldTriggerThisTurn);
    }
}
