using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>发射箭矢后为队友提供格挡（原版 <c>CoveringFirePower</c>，多人模式效果）。</summary>
public sealed class CoveringFirePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterArrowShot(Player owner, ArrowSlot? arrow, CardModel? source)
    {
        if (!ReferenceEquals(owner.Creature, Owner) || source is null || Owner.CombatState is null)
            return;

        foreach (Creature ally in Owner.CombatState.GetCreaturesOnSide(Owner.Side)
                     .Where(c => c.IsAlive && c.IsPlayer && !ReferenceEquals(c, Owner))
                     .ToArray())
        {
            await CreatureCmd.GainBlock(Owner.CombatState, ally, Amount, ValueProp.Unpowered, null, null);
        }
    }
}
