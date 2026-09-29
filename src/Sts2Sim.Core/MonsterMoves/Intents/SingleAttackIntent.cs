using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>逐字移植（<c>MegaCrit.Sts2.Core.MonsterMoves.Intents.SingleAttackIntent</c>）。</summary>
public class SingleAttackIntent : AttackIntent
{
    public SingleAttackIntent(int damage)
    {
        DamageCalc = () => damage;
    }

    public SingleAttackIntent(Func<decimal> damageCalc)
    {
        ArgumentNullException.ThrowIfNull(damageCalc);
        DamageCalc = damageCalc;
    }

    public override int Repeats => 1;

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) => GetSingleDamage(targets, owner);
}
