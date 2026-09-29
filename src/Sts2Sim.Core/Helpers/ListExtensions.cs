using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Helpers;

/// <summary>
/// 逐字移植自 v0.109（<c>MegaCrit.Sts2.Core.Extensions.ListExtensions</c>）。
/// 偏离 #42：<see cref="UnstableShuffle{T}"/> 委托给 Plan 01 的 <c>Rng.Shuffle</c>（两者算法逐位相同——
/// 均为标准 Fisher-Yates，从末尾向前交换），不重复实现。
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// "稳定"洗牌：先排序（消除初始顺序的影响）再做 Fisher-Yates。
    /// 只要 rng 相同，无论列表初始顺序如何，结果都相同。
    /// </summary>
    public static List<T> StableShuffle<T>(this List<T> list, Rng rng) where T : IComparable<T>
    {
        List<T> sorted = list.ToList();
        sorted.Sort();
        for (int i = 0; i < list.Count; i++)
        {
            list[i] = sorted[i];
        }
        return list.UnstableShuffle(rng);
    }

    /// <summary>
    /// "不稳定"洗牌：标准 Fisher-Yates，结果依赖列表初始顺序。
    /// </summary>
    public static List<T> UnstableShuffle<T>(this List<T> list, Rng rng)
    {
        rng.Shuffle(list);
        return list;
    }
}
