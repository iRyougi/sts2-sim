using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>下回合获得等量格挡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.BlockNextTurnPower</c>）：
/// 在自己一方回合开始清空格挡之后触发，授予 Unpowered 格挡，然后自我移除。</summary>
public sealed class BlockNextTurnPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (creature != Owner)
        {
            return;
        }

        await CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
        await PowerCmd.Remove(this);
    }
}
