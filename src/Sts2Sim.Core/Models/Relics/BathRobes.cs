using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 Thermae Nonupeipe relic (#927): once, fully heal before the first Boss combat starts.</summary>
public sealed class BathRobes : RelicModel
{
    private bool _wasUsed;

    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad()
    {
        if (_wasUsed) Status = RelicStatus.Disabled; // WasUsed setter: only the used branch assigns
    }

    public override bool IsUsedUp => _wasUsed;

    /// <summary>Native [SavedProperty] WasUsed.</summary>
    public bool WasUsed => _wasUsed;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner.Creature.IsDead)
        {
            return Task.CompletedTask;
        }

        // Source: Active while the current map point is a parent of the Boss map point, otherwise Normal.
        // Owner.RunState that is not a concrete RunState (test facades) has no map and is left untouched.
        if (Owner.RunState is RunState runState)
        {
            bool flag = runState.CurrentMapPoint is { } current && runState.Map.BossMapPoint.parents.Contains(current);
            Status = flag ? RelicStatus.Active : RelicStatus.Normal;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        if (!_wasUsed && Owner.Creature.IsAlive && Owner.RunState.CurrentRoom?.RoomType == RoomType.Boss)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
            _wasUsed = true;
            // Source WasUsed setter: if (IsUsedUp) Status = Disabled.
            Status = RelicStatus.Disabled;
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_wasUsed);
    }
}
