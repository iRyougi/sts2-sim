using System.Collections.Concurrent;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// 卡实例在 v0.113.1 原版 <c>CardModel.CompareTo</c> 最后一步的 DynamicVar BaseValue 视图。
/// </summary>
/// <remarks>
/// 原版逐个比较两边都有的键（<c>DynamicVars.Keys.Order(StringComparer.Ordinal)</c>）。只有同 Id、同升级、
/// 同附魔/苦难/费用/重放数时才走到这一步；同一类卡的键集合固定（<c>DynamicVarSet</c> 构造后不增删），
/// 字面量初值只被 <c>OnUpgrade</c> 改写，所以这些键在同升级时恒等，比较结果只取决于运行时会改写单个实例
/// BaseValue 的键。下表按 v0.113.1 源码穷举（2026-11-16，#918 计划附录 B）：直接写 BaseValue 或在
/// <c>OnUpgrade</c> 外调用 <c>UpgradeValueBy</c> 的卡，以及 CanonicalVars 读实例状态的卡。
/// 表中的卡类必须实现 <see cref="ICardComparisonBaseValueSource"/>，否则比较 fail-closed 抛异常；
/// 尚未移植的卡（ShowOff）登记在表中，移植时必须同时实现接口。原版 BaseValue 的 setter 截断到
/// 999,999,999，这里同样截断。
/// </remarks>
public static class CardComparisonBaseValues
{
    /// <summary>原版 <c>DynamicVar.BaseValue</c> setter 的上限。</summary>
    public const decimal MaxBaseValue = 999_999_999m;

    /// <summary>v0.113.1 中会被运行时改写的键，按卡类名登记；值在构造时按 Ordinal 排好。</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> MutableKeysByCardClass { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Claw"] = ["Damage"],
            ["Disintegration"] = ["DisintegrationPower"],
            ["Dowsing"] = ["Rooms"],
            ["GeneticAlgorithm"] = ["Block"],
            ["Guilty"] = ["Combats"],
            ["KinglyPunch"] = ["Damage"],
            ["Maul"] = ["Damage"],
            ["Rampage"] = ["Damage"],
            ["ShowOff"] = ["AimPower"],
            // HitCount 只经 SetRepeats 改写，v0.113.1 中 SetRepeats 没有调用方，实例间恒为 1。
            ["SovereignBlade"] = ["Damage"],
            ["TheBall"] = ["Damage"],
            ["TheScythe"] = ["Damage"],
            ["Thrash"] = ["Damage"],
            ["Wither"] = ["Damage"],
        }.ToDictionary(
            entry => entry.Key,
            entry => (IReadOnlyList<string>)entry.Value.Order(StringComparer.Ordinal).ToArray(),
            StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<string>> KeysByType = new();

    /// <summary>原版 DynamicVar 一步：逐键比较截断后的 BaseValue；表外卡类恒为 0。</summary>
    internal static int Compare(CardModel left, CardModel right)
    {
        IReadOnlyList<string> keys = KeysByType.GetOrAdd(left.GetType(), ResolveKeys);
        if (keys.Count == 0)
        {
            return 0;
        }

        var leftSource = (ICardComparisonBaseValueSource)left;
        var rightSource = (ICardComparisonBaseValueSource)right;
        foreach (string key in keys)
        {
            int result = Clamp(leftSource.GetComparisonBaseValue(key))
                .CompareTo(Clamp(rightSource.GetComparisonBaseValue(key)));
            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }

    private static decimal Clamp(decimal value) => Math.Min(value, MaxBaseValue);

    private static IReadOnlyList<string> ResolveKeys(Type type)
    {
        if (!MutableKeysByCardClass.TryGetValue(type.Name, out IReadOnlyList<string>? keys))
        {
            return Array.Empty<string>();
        }

        if (!typeof(ICardComparisonBaseValueSource).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"{type.Name} has runtime-mutable DynamicVar keys ({string.Join(", ", keys)}) in v0.113.1 " +
                $"CardModel.CompareTo but does not implement {nameof(ICardComparisonBaseValueSource)}.");
        }

        return keys;
    }
}

/// <summary>按原版键名返回当前实例的 DynamicVar BaseValue；只需支持 <see cref="CardComparisonBaseValues"/>
/// 为该卡类登记的键，其余键抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
public interface ICardComparisonBaseValueSource
{
    decimal GetComparisonBaseValue(string key);
}
