namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;

public sealed class BurrowedPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override bool ShouldClearBlock(Creature creature) => creature != Owner;

    public override async Task AfterBlockBroken(Creature target, Creature? breaker)
    {
        if (target != Owner || !Owner.Powers.Contains(this) || Owner.Monster is not Tunneler tunneler) return;
        tunneler.GetStunned();
        tunneler.SetBurrowBreakStunned();
        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        // Upstream LoseBlock rejects dead creatures before touching their combat state.
        // The local command requires a nonnull state at entry, so preserve that guard here.
        if (oldOwner.IsDead || oldOwner.CombatState is not { } combatState || !combatState.IsLiveCombat())
            return Task.CompletedTask;
        return oldOwner.Block > 0
            ? CreatureCmd.LoseBlock(combatState, oldOwner, oldOwner.Block, null)
            : Task.CompletedTask;
    }
}
