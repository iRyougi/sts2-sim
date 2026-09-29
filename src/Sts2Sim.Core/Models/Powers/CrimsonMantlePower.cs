using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class CrimsonMantlePower : PowerModel
{
    private decimal _selfDamage;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner.Player) return;
        await CreatureCmd.Damage(Owner.CombatState!, [Owner], _selfDamage,
            ValueProp.Unblockable | ValueProp.Unpowered, Owner, null, null);
        await CreatureCmd.GainBlock(Owner.CombatState!, Owner, Amount, ValueProp.Unpowered, null, null);
    }

    public void IncrementSelfDamage() { AssertMutable(); _selfDamage++; }

    internal override void AppendCombatStateDescription(ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => builder.Append(_selfDamage);
}
