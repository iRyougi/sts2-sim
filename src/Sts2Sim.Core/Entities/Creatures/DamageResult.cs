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

    /// <summary>Native block result after all HP-loss modifiers, including zero-damage hits into Block.</summary>
    public bool WasFullyBlocked { get; init; }

    public bool WasTargetKilled { get; init; }

    public int TotalDamage => BlockedDamage + UnblockedDamage;
}
