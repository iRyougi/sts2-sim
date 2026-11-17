using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Metronome : RelicModel
{
    private const int OrbCount = 7; // DynamicVar "OrbCount"

    private int _orbsChanneled;

    // Metronome.UpdateDisplay (IsActivating is a 1s animation flag; always false at a decision point).
    private void UpdateStatus() =>
        Status = _orbsChanneled == OrbCount - 1 ? RelicStatus.Active : RelicStatus.Normal;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            _orbsChanneled = 0;
            UpdateStatus();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterOrbChanneled(Player player, OrbModel orb)
    {
        if (player != Owner) return;
        _orbsChanneled++;
        UpdateStatus();
        if (_orbsChanneled != 7) return;
        Flash();
        await CreatureCmd.Damage(Owner.Creature.CombatState!,
            Owner.Creature.CombatState!.HittableEnemies,
            30m, ValueProp.Unpowered, Owner.Creature, null, null);
    }

    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        _orbsChanneled = 0;
        UpdateStatus();
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_orbsChanneled);
}
