using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Helpers;

/// <summary>
/// 逐字移植自 v0.113.1（<c>MegaCrit.Sts2.Core.Extensions.ListExtensions</c> 与
/// <c>IEnumerableExtensions.TakeRandom</c>）。v0.113.1 删除了 <c>Rng.Shuffle</c>，
/// 所有洗牌统一走 <see cref="UnstableShuffle{T}"/>：先从传入的 rng 取一次
/// <c>NextUnsignedLong()</c> 派生子 Rng，再用子 Rng 做 Fisher-Yates（#946）。
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// "稳定"洗牌：先排序（消除初始顺序的影响）再做 <see cref="UnstableShuffle{T}"/>。
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
    /// "不稳定"洗牌：结果依赖列表初始顺序。父流只消耗一次 <c>NextUnsignedLong()</c>；
    /// Fisher-Yates 的 n-1 次 <c>NextInt</c> 全部落在派生出的子 Rng 上。
    /// 重键模式（偏离 #329）下传入的 rng 是按语义键派生的独立流，子 Rng 的派生方式与顺序模式相同。
    /// </summary>
    public static List<T> UnstableShuffle<T>(this List<T> list, Rng rng)
    {
        Rng child = new Rng(rng.NextUnsignedLong());
        int num = list.Count;
        while (num > 1)
        {
            num--;
            int num2 = child.NextInt(num + 1);
            T value = list[num];
            T value2 = list[num2];
            list[num2] = value;
            list[num] = value2;
        }
        return list;
    }

    /// <summary>原版 <c>IEnumerableExtensions.TakeRandom</c>：复制成列表后不稳定洗牌，再取前 count 个。</summary>
    public static IEnumerable<T> TakeRandom<T>(this IEnumerable<T> collection, int count, Rng rng)
    {
        return collection.ToList().UnstableShuffle(rng).Take(count);
    }
}
