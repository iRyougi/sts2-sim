using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class RitualPower : PowerModel
{
    private bool _skipFirstEnemyTurnEnd;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _skipFirstEnemyTurnEnd = Owner.Side == CombatSide.Enemy;
        return Task.CompletedTask;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || Owner.IsDead)
        {
            return;
        }

        if (_skipFirstEnemyTurnEnd)
        {
            _skipFirstEnemyTurnEnd = false;
            return;
        }

        await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Amount, Owner, null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_skipFirstEnemyTurnEnd);
    }
}
