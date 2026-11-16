using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 Museum of Torture relic (#927): in Elite rooms, apply 2 Weak to all hittable enemies before the
/// owner's first turn.</summary>
public sealed class TormentousCollar : RelicModel
{
    private const decimal Weak = 2m;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (Owner.RunState.CurrentRoom is { RoomType: RoomType.Elite } && participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState!.TurnNumber <= 1)
        {
            Flash();
            ICombatState combatState = Owner.Creature.CombatState!;
            // Native PowerCmd.Apply(IEnumerable) walks the live HittableEnemies one target at a time; the snapshot
            // differs only if applying Weak changed hittability, which no current content does.
            foreach (Creature enemy in combatState.HittableEnemies.ToArray())
                await PowerCmd.Apply<WeakPower>(combatState, enemy, Weak, Owner.Creature, null);
        }
    }
}
