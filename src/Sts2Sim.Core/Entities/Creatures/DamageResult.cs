using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Entities.Creatures;

/// <summary>一次伤害结算的结果。逐字移植自 v0.109（<c>MegaCrit.Sts2.Core.Entities.Creatures.DamageResult</c>）。</summary>
public sealed record DamageResult(Creature Receiver, ValueProp Props)
{
    /// <summary><see cref="Creature.LoseHpInternal"/> 不会填充此字段（恒为 0）；调用方需自行拼接
    /// <see cref="Creature.DamageBlockInternal"/> 返回的格挡量,通过 <c>with { BlockedDamage = ... }</c> 补全。</summary>
    public int BlockedDamage { get; init; }

    public int UnblockedDamage { get; init; }

    public int OverkillDamage { get; init; }

    public bool WasTargetKilled { get; init; }

    /// <summary>原版只在原始目标那一条结果上设置：原始目标结算后格挡 ≤ 0 且本次有伤害被格挡。
    /// 宠物被命中时格挡由主人承担，宠物自身格挡恒为 0，所以只要有伤害被格挡就算打破。</summary>
    public bool WasBlockBroken { get; init; }

    /// <summary>原版只在原始目标那一条结果上设置：非不可格挡、有伤害被格挡或原始目标仍有格挡，且承受者最终扣血为 0。
    /// 伤害转给 Osty 时按 Osty 的扣血判断，而不是原始目标的溢出伤害。</summary>
    public bool WasFullyBlocked { get; init; }

    public int TotalDamage => BlockedDamage + UnblockedDamage;
}
