using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Creatures;

/// <summary>对单个目标的一次伤害拆出的全部结果。逐字移植 v0.114.0
/// <c>MegaCrit.Sts2.Core.Entities.Creatures.DamageResults</c>：原目标必有，宠物主人格挡、格挡前转移、格挡后转移三项可空。</summary>
public readonly record struct DamageResults
{
    public required DamageResult Original { get; init; }

    public DamageResult? BlockRedirect { get; init; }

    public DamageResult? PreBlockRedirect { get; init; }

    public DamageResult? UnblockedRedirect { get; init; }

    public required IReadOnlyList<AbstractModel> BeforeWuppyHpLossModifiers { get; init; }

    public required IReadOnlyList<AbstractModel> BeforeOstyHpLossModifiers { get; init; }

    public required IReadOnlyList<AbstractModel> AfterOstyHpLossModifiers { get; init; }

    public required IReadOnlyList<AbstractModel> AfterOstyHpLossModifiers2 { get; init; }

    /// <summary>原版遍历顺序：原目标 → 宠物主人格挡 → 格挡前转移 → 格挡后转移。</summary>
    public IEnumerable<DamageResult> AllResults()
    {
        yield return Original;
        if (BlockRedirect is not null)
            yield return BlockRedirect;
        if (PreBlockRedirect is not null)
            yield return PreBlockRedirect;
        if (UnblockedRedirect is not null)
            yield return UnblockedRedirect;
    }
}
