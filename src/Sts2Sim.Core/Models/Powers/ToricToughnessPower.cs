using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ToricToughnessPower : PowerModel
{
    public decimal StoredBlock { get; private set; }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public void StoreBlock(decimal block)
    {
        AssertMutable();
        StoredBlock = block;
    }

    public override async Task AfterBlockCleared(Creature creature)
    {
        if (!ReferenceEquals(creature, Owner))
        {
            return;
        }

        await CreatureCmd.GainBlock(
            Owner.CombatState!,
            Owner,
            StoredBlock,
            ValueProp.Unpowered,
            null,
            null);
        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(StoredBlock);
    }
}
