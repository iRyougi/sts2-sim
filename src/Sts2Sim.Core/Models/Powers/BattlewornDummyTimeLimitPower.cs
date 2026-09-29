namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

public sealed class BattlewornDummyTimeLimitPower : PowerModel
{
    private bool _shouldEscape;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public bool HasExpired => _shouldEscape;

    public bool ShouldEscape => _shouldEscape;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner) || Owner.IsDead)
        {
            return;
        }

        if (Amount > 1)
        {
            await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
            return;
        }

        _shouldEscape = true;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_shouldEscape);
}
