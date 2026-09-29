using Sts2Sim.Core.Commands.Builders;

namespace Sts2Sim.Core.Commands;

public static class DamageCmd
{
    public static AttackCommand Attack(decimal damagePerHit) => new(damagePerHit);
}
