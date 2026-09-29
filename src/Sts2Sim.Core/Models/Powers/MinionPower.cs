namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Entities.Powers;

/// <summary>Marks the owner as a secondary/minion enemy for downstream content.</summary>
public sealed class MinionPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool OwnerIsSecondaryEnemy => true;

    public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;

    public override bool ShouldOwnerDeathTriggerFatal() => false;
}
