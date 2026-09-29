using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class PoisonPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    private int TriggerCount
    {
        get
        {
            int accelerant = Owner.CombatState!
                .GetOpponentsOf(Owner)
                .Where(creature => creature.IsAlive)
                .Sum(creature => creature.Powers.OfType<AccelerantPower>().Sum(power => power.Amount));
            return Math.Min(Amount, 1 + accelerant);
        }
    }

    public int CalculateTotalDamageNextTurn()
    {
        decimal total = 0m;
        int triggerCount = TriggerCount;
        for (int index = 0; index < triggerCount; index++)
        {
            total += Hook.ModifyDamage(
                Owner.CombatState!,
                Owner,
                dealer: null,
                Amount - index,
                ValueProp.Unblockable | ValueProp.Unpowered,
                cardSource: null,
                cardPlay: null,
                out _);
        }

        return (int)total;
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await Trigger();
        }
    }

    public async Task Trigger()
    {
        int triggerCount = TriggerCount;
        for (int index = 0; index < triggerCount; index++)
        {
            await CreatureCmd.Damage(
                Owner.CombatState!,
                new[] { Owner },
                Amount,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: null,
                cardSource: null,
                cardPlay: null);
            if (!Owner.IsAlive)
            {
                return;
            }

            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        }
    }
}
