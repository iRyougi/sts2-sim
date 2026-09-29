namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

public sealed class AsleepPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage == 0)
        {
            return;
        }

        if (Owner.GetPower<PlatingPower>() is { } plating)
        {
            await PowerCmd.Remove(plating);
        }

        LagavulinMatriarch monster = (LagavulinMatriarch)(Owner.Monster
            ?? throw new InvalidOperationException("AsleepPower requires a Lagavulin Matriarch owner."));
        monster.IsAwake = true;
        monster.SetWakeUpStunned();
        await PowerCmd.Remove(this);
    }

    public override async Task BeforeSideTurnEndVeryEarly(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner) &&
            Amount <= 1 &&
            Owner.GetPower<PlatingPower>() is { } plating)
        {
            await PowerCmd.Remove(plating);
        }
    }

    public override async Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        if (Amount <= 0)
        {
            LagavulinMatriarch monster = (LagavulinMatriarch)(Owner.Monster
                ?? throw new InvalidOperationException("AsleepPower requires a Lagavulin Matriarch owner."));
            await monster.WakeUpMove(Array.Empty<Creature>());
        }
    }
}
