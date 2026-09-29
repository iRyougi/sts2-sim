namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Combat.StateDescription;

public sealed class ReattachPower : PowerModel
{
    private bool _isReviving;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public bool IsReviving => _isReviving;

    public override Task AfterDeath(Creature target)
    {
        if (!ReferenceEquals(target, Owner) || AreAllOtherSegmentsDead())
        {
            return Task.CompletedTask;
        }

        _isReviving = true;
        DecimillipedeSegment segment = (DecimillipedeSegment)Owner.Monster!;
        Owner.Monster!.SetMoveImmediate(segment.DeadState, forceTransition: true);
        return Task.CompletedTask;
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        !ReferenceEquals(creature, Owner) || !_isReviving;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        !ReferenceEquals(creature, Owner);

    public override bool ShouldPowerBeRemovedOnDeath(PowerModel power) =>
        !ReferenceEquals(power, this);

    public override bool ShouldOwnerDeathTriggerFatal() => AreAllOtherSegmentsDead();

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => builder.Append(_isReviving);

    public async Task DoReattach()
    {
        if (AreAllOtherSegmentsDead())
        {
            return;
        }

        _isReviving = false;
        await CreatureCmd.Heal(Owner, Amount);
    }

    private bool AreAllOtherSegmentsDead() => Owner.CombatState!
        .GetCreaturesOnSide(Owner.Side)
        .Where(creature => !ReferenceEquals(creature, Owner) && creature.Monster is DecimillipedeSegment)
        .All(creature => creature.IsDead);
}
