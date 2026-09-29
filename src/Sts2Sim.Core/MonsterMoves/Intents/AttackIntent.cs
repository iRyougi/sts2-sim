using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>攻击类意图基类。偏离 #34：GetSingleDamage 跳过原版的 CardPreviewMode.MultiCreatureTargeting 悬停预览分支,直接读 DamageCalc()。</summary>
public abstract class AttackIntent : AbstractIntent
{
    public override IntentType IntentType => IntentType.Attack;

    public Func<decimal>? DamageCalc { get; protected set; }

    public abstract int Repeats { get; }

    public abstract int GetTotalDamage(IEnumerable<Creature> targets, Creature owner);

    public int GetSingleDamage(IEnumerable<Creature> targets, Creature owner)
    {
        return Math.Max(0, (int)DamageCalc!());
    }
}
