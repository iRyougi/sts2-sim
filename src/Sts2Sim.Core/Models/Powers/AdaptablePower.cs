namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;

public sealed class AdaptablePower : PowerModel
{
    private bool _isReviving;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public bool IsReviving => _isReviving;

    public void DoRevive()
    {
        AssertMutable();
        _isReviving = false;
    }

    public override async Task AfterDeath(Creature target)
    {
        if (target != Owner || _isReviving || target.Monster is not TestSubject testSubject)
        {
            return;
        }

        _isReviving = true;
        await testSubject.TriggerDeadState();
    }

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Owner || !_isReviving;

    public override bool ShouldStopCombatFromEnding() => true;

    public override bool ShouldCreatureBeRemovedFromCombatAfterDeath(Creature creature) =>
        creature != Owner;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_isReviving);
}
