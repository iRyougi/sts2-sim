using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 Thermae Nonupeipe relic (#927): once, fully heal before the first Boss combat starts.</summary>
public sealed class BathRobes : RelicModel
{
    private bool _wasUsed;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsUsedUp => _wasUsed;

    /// <summary>Native [SavedProperty] WasUsed.</summary>
    public bool WasUsed => _wasUsed;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        // Native updates only the relic's presentation status (active next to the Boss) here.
        return Task.CompletedTask;
    }

    public override async Task BeforeCombatStart()
    {
        if (!_wasUsed && Owner.Creature.IsAlive && Owner.RunState.CurrentRoom?.RoomType == RoomType.Boss)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
            _wasUsed = true;
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_wasUsed);
    }
}
