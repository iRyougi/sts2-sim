using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Entities.Creatures;

/// <summary>一次伤害结算的结果。逐字移植自 v0.109（v0.114.0 未变）（<c>MegaCrit.Sts2.Core.Entities.Creatures.DamageResult</c>）。</summary>
public sealed record DamageResult(Creature Receiver, ValueProp Props)
{
    /// <summary>Original recipient before pre-block or unblocked damage redirection.</summary>
    public Creature OriginalTarget { get; init; } = Receiver;

    /// <summary>HP loss before HP-loss reducers, after block for the ordinary receiver.</summary>
    public int IntendedHpLoss { get; init; }

    /// <summary>被格挡的伤害量。只在原目标与宠物主人格挡（blockRedirect）两条结果上设置。</summary>
    public int BlockedDamage { get; init; }

    public int UnblockedDamage { get; init; }

    public int OverkillDamage { get; init; }

    public bool WasTargetKilled { get; init; }

    /// <summary>v0.114.0：在空跑阶段判定，<c>target.Block &lt;= blocked &amp;&amp; blocked &gt; 0</c>（target 为原始目标，
    /// 判定时格挡尚未扣减）。原目标与宠物主人格挡两条结果上设置。</summary>
    public bool WasBlockBroken { get; init; }

    /// <summary>非不可格挡、有伤害被格挡或原始目标有格挡，且承受者最终扣血为 0（伤害转给 Osty 时按 Osty 的扣血判断）。
    /// 原目标与宠物主人格挡两条结果上设置。</summary>
    public bool WasFullyBlocked { get; init; }

    public int TotalDamage => BlockedDamage + UnblockedDamage;
}
