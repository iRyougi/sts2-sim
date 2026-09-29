using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class PlatingPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        if (Owner.Side == CombatSide.Enemy)
        {
            int playerCount = Math.Max(1, Owner.CombatState!.RunState.Players.Count);
            SetAmount(Amount * ((playerCount - 1) * 2 + 1));
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (side != CombatSide.Player || Owner.Side != CombatSide.Enemy ||
            Owner.CombatState!.RoundNumber > 1)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(
            Owner.CombatState,
            Owner,
            Amount,
            ValueProp.Unpowered,
            null,
            null);
    }

    public override async Task BeforeSideTurnEndEarly(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await CreatureCmd.GainBlock(
                Owner.CombatState!,
                Owner,
                Amount,
                ValueProp.Unpowered,
                null,
                null);
        }
    }

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        bool isFirstPlayerTurn = Owner.Player?.PlayerCombatState?.TurnNumber == 1;
        bool isFirstEnemyTurn = Owner.Side == CombatSide.Enemy &&
                                Owner.CombatState!.RoundNumber == 1;
        if (!isFirstPlayerTurn && !isFirstEnemyTurn)
        {
            int decrement = Owner.Side == CombatSide.Enemy
                ? Owner.CombatState!.RunState.Players.Count
                : 1;
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -decrement, null, null);
        }
    }
}
