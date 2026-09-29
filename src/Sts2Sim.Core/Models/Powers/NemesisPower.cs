namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

public sealed class NemesisPower : PowerModel
{
    private bool _shouldApplyIntangible;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return;
        }

        _shouldApplyIntangible = !_shouldApplyIntangible;
        if (_shouldApplyIntangible)
        {
            await PowerCmd.Apply<IntangiblePower>(Owner.CombatState!, Owner, 1m, Owner, null);
        }
        else if (Owner.GetPower<IntangiblePower>() is { } intangible)
        {
            await PowerCmd.Remove(intangible);
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_shouldApplyIntangible);
}
