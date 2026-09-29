using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Orichalcum : RelicModel
{
    private bool _shouldTrigger;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _shouldTrigger = false;
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnEndEarly(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) &&
            Owner.Creature.Block == 0)
        {
            _shouldTrigger = true;
        }

        return Task.CompletedTask;
    }

    public override async Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!_shouldTrigger)
        {
            return;
        }

        _shouldTrigger = false;
        await CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            6m,
            ValueProp.Unpowered,
            null,
            null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_shouldTrigger);
    }
}
