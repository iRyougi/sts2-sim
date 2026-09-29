using System.Globalization;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Random;

/// <summary>
/// A custom random class which allows predictable results when utilizing seeds.
/// Vendored verbatim from v0.109 game source (MegaCrit.Sts2.Core.Random.Rng), with
/// deviations registered in Plan 02.5's deviation list:
///  - #20/#23: <c>Chaotic</c> (wall-clock, non-deterministic) and the
///    <c>Rng(Player, ModelId, mixin)</c> ergonomic constructor are omitted — <c>Player</c>
///    is not yet ported (Plan 03), and wall-clock RNG has no place in a deterministic sim.
///  - #22: <c>_counter</c> is exposed as a public read-only <see cref="Counter"/> property
///    instead of a private field (tests/diagnostics need visibility; ToSerializable is
///    unchanged).
///  - #21/#189: <see cref="CloneExact()"/> 与 <see cref="CloneReseeded(ulong)"/> 均非游戏原有；
///    前者用于 O(1) 并行环境快照，后者用于战略层 playout 的确定化采样。
///  - #329: semantic-key-required/observed generators are absent from the game and are created only
///    by the explicitly named label-generation path. They are not a Boolean flavor of CloneExact or
///    CloneReseeded because selecting the wrong semantics would succeed silently and be hard to diagnose.
/// </summary>
public class Rng
{
    public int Counter { get; private set; }

    /// <summary>Diagnostic identity for RNGs outside the run/player stream sets.</summary>
    public string? DiagnosticStreamName { get; private set; }

    private readonly MegaRandom _random;
    private readonly bool _semanticKeyRequired;
    private readonly Action<int, string, string>? _drawObserver;

    public Rng(ulong seed = 0uL)
    {
        _random = new MegaRandom(seed);
    }

    public Rng(SerializableRng serializable)
    {
        Counter = serializable.counter;
        _random = new MegaRandom(serializable);
    }

    /// <summary>
    /// An ergonomic constructor for creating an RNG for a specific piece of content during a run.
    /// </summary>
    public Rng(ulong seed, string name)
        : this(seed + StringHelper.GetDeterministicHashCode(name))
    {
        if (RngDiagnostics.DrawObserver is not null)
            DiagnosticStreamName = "named." + name;
    }

    /// <summary>Annotate a content RNG without changing its seed or draw sequence.</summary>
    public Rng WithDiagnosticContentId(string modelId)
    {
        if (RngDiagnostics.DrawObserver is not null)
            DiagnosticStreamName = "content." + modelId;
        return this;
    }

    /// <summary>
    /// Deviation #21: exact, independent snapshot of the generator's internal state (counter +
    /// underlying <see cref="MegaRandom"/> state). Not present in the game; used for O(1)
    /// parallel environment resets.
    /// </summary>
    private Rng(
        MegaRandom random,
        int counter,
        bool semanticKeyRequired = false,
        Action<int, string, string>? drawObserver = null)
    {
        _random = random;
        Counter = counter;
        _semanticKeyRequired = semanticKeyRequired;
        _drawObserver = drawObserver;
    }

    internal static Rng CreateSemanticKeyRequired(ulong seed) =>
        new(new MegaRandom(seed), counter: 0, semanticKeyRequired: true);

    internal static Rng CreateObserved(
        ulong seed,
        Action<int, string, string> drawObserver) =>
        new(new MegaRandom(seed), counter: 0, drawObserver: drawObserver);

    /// <summary>
    /// 精确克隆：保留 counter 与底层生成器状态，克隆体与源在后续调用中产出完全相同的序列。
    /// 用于"分支必须看到与真实流一致的未来"的场景——战斗搜索的所有分支都属于这一类。
    /// 偏离 #21 的延续。
    /// </summary>
    public Rng CloneExact()
    {
        return new Rng(_random.Clone(), Counter, _semanticKeyRequired, _drawObserver)
        {
            DiagnosticStreamName = DiagnosticStreamName
        };
    }

    private void RequireSequentialDrawAllowed()
    {
        if (_semanticKeyRequired)
        {
            throw new InvalidOperationException(
                "This keyed RNG stream requires a semantic key. Use the owning RNG set's ForSemanticKey method.");
        }
    }

    private void RecordDraw<T>(string operation, T result)
    {
        _drawObserver!.Invoke(
            Counter,
            operation,
            Convert.ToString(result, CultureInfo.InvariantCulture) ?? string.Empty);
    }

    /// <summary>
    /// 重播种派生：丢弃源的 counter 与生成器状态，用 <paramref name="branchSeed"/> 重新播种，
    /// counter 归零。用于"需要对未知量取多个独立样本求期望"的场景——战略层 playout 的确定化采样。
    /// <para>
    /// 偏离 #189：真实游戏没有这个操作。它与 <see cref="CloneExact"/> 是<b>互斥语义</b>，
    /// 刻意做成两个不同的方法名而不是一个带布尔参数的方法，因为误用不会报错、
    /// 只会表现为"搜索加深了但效果没提升"，极难定位（见 Plan08b 设计简报 A.3）。
    /// </para>
    /// </summary>
    /// <param name="branchSeed">该分支的独立种子。同一个种子必然产出同一条序列。</param>
    public Rng CloneReseeded(ulong branchSeed)
    {
        var clone = new Rng(branchSeed);
        if (RngDiagnostics.DrawObserver is not null) clone.DiagnosticStreamName = "nongameplay";
        return clone;
    }

    /// <summary>
    /// 带流名的重播种派生：与 <see cref="CloneReseeded(ulong)"/> 相同，但按流名再做一次确定性哈希偏移，使同一个 <paramref name="branchSeed"/> 下的不同流拿到不同序列。
    /// 派生方式与 <c>RunRngSet</c>/<c>PlayerRngSet</c> 构造流的方式一致。偏离 #189。
    /// </summary>
    public Rng CloneReseeded(ulong branchSeed, string streamName)
    {
        var clone = new Rng(branchSeed, streamName);
        if (RngDiagnostics.DrawObserver is not null) clone.DiagnosticStreamName = "nongameplay";
        return clone;
    }

    /// <summary>
    /// Load RNG state from serializable.
    /// Use this when you need to keep the old instance's references intact.
    /// </summary>
    public void LoadFromSerializable(SerializableRng serializable)
    {
        Counter = serializable.counter;
        _random.Reinitialise(serializable);
    }

    /// <summary>
    /// Returns true or false. 50/50
    /// </summary>
    public bool NextBool()
    {
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextBool", 0, 0, 0);
        bool result = _random.Next(2) == 0;
        if (_drawObserver is not null) RecordDraw("NextBool", result);
        return result;
    }

    /// <summary>
    /// Get a random integer between 0 (inclusive) and maxExclusive.
    /// </summary>
    /// <param name="maxExclusive">1 higher than the highest allowed int.</param>
    /// <returns>Random integer.</returns>
    public int NextInt(int maxExclusive = int.MaxValue)
    {
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextInt", 0, maxExclusive, 1);
        int result = _random.Next(maxExclusive);
        if (_drawObserver is not null)
        {
            RecordDraw($"NextInt(maxExclusive={maxExclusive})", result);
        }
        return result;
    }

    /// <summary>
    /// Get a random integer between minInclusive and maxExclusive.
    /// </summary>
    /// <param name="minInclusive">Lowest allowed number.</param>
    /// <param name="maxExclusive">1 higher than the maximum allowed number.</param>
    /// <returns>Random integer.</returns>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "Minimum must be lower than maximum.");
        }
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextInt", minInclusive, maxExclusive, 2);
        int result = _random.Next(minInclusive, maxExclusive);
        if (_drawObserver is not null)
        {
            RecordDraw(
                $"NextInt(minInclusive={minInclusive},maxExclusive={maxExclusive})",
                result);
        }
        return result;
    }

    /// <summary>
    /// Get a random integer between 0 (inclusive) and maxExclusive.
    /// </summary>
    /// <param name="maxExclusive">1 higher than the highest allowed int.</param>
    /// <returns>Random integer.</returns>
    public uint NextUnsignedInt(uint maxExclusive = uint.MaxValue)
    {
        return NextUnsignedInt(0u, maxExclusive);
    }

    /// <summary>
    /// Get a random integer between minInclusive and maxInclusive.
    /// </summary>
    /// <param name="minInclusive">Lowest allowed number.</param>
    /// <param name="maxExclusive">1 higher than the maximum allowed number.</param>
    /// <returns>Random integer.</returns>
    public uint NextUnsignedInt(uint minInclusive, uint maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "Minimum must be lower than maximum.");
        }
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextUnsignedInt", minInclusive, maxExclusive, 2);
        double num = _random.NextDouble();
        double num2 = maxExclusive - minInclusive;
        uint num3 = (uint)(num * num2);
        uint result = minInclusive + num3;
        if (_drawObserver is not null)
        {
            RecordDraw(
                $"NextUnsignedInt(minInclusive={minInclusive},maxExclusive={maxExclusive})",
                result);
        }
        return result;
    }

    public ulong NextUnsignedLong()
    {
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextUnsignedLong", 0, 0, 0);
        ulong result = _random.NextULong();
        if (_drawObserver is not null) RecordDraw("NextUnsignedLong", result);
        return result;
    }

    public ulong NextUnsignedLong(ulong maxExclusive = ulong.MaxValue)
    {
        if (maxExclusive == ulong.MaxValue)
        {
            return NextUnsignedLong();
        }
        return NextUnsignedLong(0uL, maxExclusive);
    }

    public ulong NextUnsignedLong(ulong minInclusive, ulong maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(minInclusive), "Minimum must be lower than maximum.");
        }
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextUnsignedLong", 0, 0, -1);
        double num = _random.NextDouble();
        double num2 = maxExclusive - minInclusive;
        ulong num3 = (ulong)(num * num2);
        ulong result = minInclusive + num3;
        if (_drawObserver is not null)
        {
            RecordDraw(
                $"NextUnsignedLong(minInclusive={minInclusive},maxExclusive={maxExclusive})",
                result);
        }
        return result;
    }

    /// <summary>
    /// Get a random floating-point between 0 and max.
    /// </summary>
    /// <param name="max">Highest allowed number.</param>
    /// <returns>Random float.</returns>
    public float NextFloat(float max = 1f)
    {
        return NextFloat(0f, max);
    }

    /// <summary>
    /// Get a random floating-point number between min and max.
    /// </summary>
    /// <param name="min">Lowest allowed number.</param>
    /// <param name="max">Highest allowed number.</param>
    /// <returns>Random float.</returns>
    public float NextFloat(float min, float max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must not be higher than maximum.");
        }
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextFloat", min, max, 2);
        float result = (float)(_random.NextDouble() * (double)(max - min) + (double)min);
        if (_drawObserver is not null)
        {
            RecordDraw(
                $"NextFloat(min={min.ToString(CultureInfo.InvariantCulture)},max={max.ToString(CultureInfo.InvariantCulture)})",
                result);
        }
        return result;
    }

    /// <summary>
    /// Get a random double-precision (double) between 0.0 (inclusive) and 1.0 (exclusive).
    /// </summary>
    /// <returns>Random double.</returns>
    public double NextDouble()
    {
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextDouble", 0, 0, 0);
        double result = _random.NextDouble();
        if (_drawObserver is not null) RecordDraw("NextDouble", result);
        return result;
    }

    /// <summary>
    /// Get a random double-precision floating-point number between 0 and 1.
    /// </summary>
    /// <returns>Random double.</returns>
    public double NextDouble(double min, double max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must not be higher than maximum.");
        }
        RequireSequentialDrawAllowed();
        Counter++;
        RngDiagnostics.DrawObserver?.Invoke(this, "NextDouble", min, max, 2);
        double result = _random.NextDouble() * (max - min) + min;
        if (_drawObserver is not null)
        {
            RecordDraw(
                $"NextDouble(min={min.ToString(CultureInfo.InvariantCulture)},max={max.ToString(CultureInfo.InvariantCulture)})",
                result);
        }
        return result;
    }

    public float NextGaussianFloat(float mean = 0f, float stdDev = 1f, float min = 0f, float max = 1f)
    {
        return (float)NextGaussianDouble(mean, stdDev, min, max);
    }

    /// <summary>
    /// Generate a random floating-point number with a Gaussian distribution between min and max with the specified mean
    /// and standard deviation.
    /// See https://en.wikipedia.org/wiki/Normal_distribution for more on Gaussian distributions.
    /// </summary>
    /// <param name="mean">Mean for the Gaussian distribution.</param>
    /// <param name="stdDev">Standard deviation for the Gaussian distribution.</param>
    /// <param name="min">Lowest allowed number.</param>
    /// <param name="max">Highest allowed number.</param>
    /// <returns>Random double with a Gaussian distribution.</returns>
    public double NextGaussianDouble(double mean = 0.0, double stdDev = 1.0, double min = 0.0, double max = 1.0)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must not be higher than maximum.");
        }
        if (mean < 0.0 || mean > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be within [0, 1].");
        }
        double num4;
        do
        {
            double d = 1.0 - NextDouble();
            double num = 1.0 - NextDouble();
            double num2 = Math.Sqrt(-2.0 * Math.Log(d));
            double d2 = Math.PI * 2.0 * num;
            double num3 = num2 * Math.Cos(d2);
            num4 = mean + num3 * stdDev;
        }
        while (num4 < 0.0 || num4 > 1.0);
        return num4 * (max - min) + min;
    }

    /// <summary>
    /// Returns a random number within a normal distribution.
    /// This is used as part of the algorithm for location assignment.
    /// </summary>
    /// <param name="mean">Mean for the Gaussian distribution.</param>
    /// <param name="stdDev">Standard deviation for the Gaussian distribution.</param>
    /// <param name="min">Lowest allowed number.</param>
    /// <param name="max">Highest allowed number.</param>
    /// <returns></returns>
    public int NextGaussianInt(int mean, int stdDev, int min, int max)
    {
        if (min > max)
        {
            throw new ArgumentOutOfRangeException(nameof(min), "Minimum must not be higher than maximum.");
        }
        if (mean < min || mean > max)
        {
            throw new ArgumentOutOfRangeException(nameof(mean), mean, "Mean must be within [min, max].");
        }
        int num3;
        do
        {
            double d = 1.0 - NextDouble();
            double num = 1.0 - NextDouble();
            double num2 = Math.Sqrt(-2.0 * Math.Log(d)) * Math.Sin(Math.PI * 2.0 * num);
            double a = (double)mean + (double)stdDev * num2;
            num3 = (int)Math.Round(a);
        }
        while (num3 < min || num3 > max);
        return num3;
    }

    /// <summary>
    /// Get a random item from the specified set of items.
    /// </summary>
    /// <param name="items">Set of items to pull from.</param>
    /// <typeparam name="T">Type of items contained in the set.</typeparam>
    /// <returns>Random item.</returns>
    public T? NextItem<T>(IEnumerable<T> items)
    {
        IEnumerable<T> source = (items as T[]) ?? items.ToArray();
        int num = source.Count();
        if (num == 0)
        {
            return default;
        }
        int index = NextInt(0, num);
        return source.ElementAt(index);
    }

    /// <summary>
    /// Get a random item from the specified set of items, respecting a specified weighting function.
    /// </summary>
    /// <param name="items">Set of items to pull from.</param>
    /// <param name="weightFetcher">Function to determine which items should be returned more/less often.</param>
    /// <typeparam name="T">Type of items contained in the set.</typeparam>
    /// <returns>Random item.</returns>
    public T? WeightedNextItem<T>(IEnumerable<T> items, Func<T?, float> weightFetcher)
    {
        return WeightedNextItem(NextFloat(), items, weightFetcher!, default(T)!);
    }

    /// <summary>
    /// Get a random item from the specified set of items, respecting a specified weighting function.
    /// </summary>
    /// <param name="randInput">Floating-point number between 0 and 1 to use for randomness.</param>
    /// <param name="items">Set of items to pull from.</param>
    /// <param name="weightFetcher">Function to determine which items should be returned more/less often.</param>
    /// <param name="fallback">Element to return if the weighting function fails to grab an item.</param>
    /// <typeparam name="T">Type of items contained in the set.</typeparam>
    /// <returns>Random item.</returns>
    public static T WeightedNextItem<T>(float randInput, IEnumerable<T> items, Func<T, float> weightFetcher, T fallback)
    {
        float num = items.Sum(weightFetcher);
        float num2 = randInput * num;
        foreach (T item in items)
        {
            num2 -= weightFetcher(item);
            if (num2 <= 0f)
            {
                return item;
            }
        }
        return fallback;
    }

    /// <summary>
    /// Shuffles a list in place using the Fisher-Yates algorithm.
    /// </summary>
    /// <param name="list">The list to shuffle.</param>
    /// <typeparam name="T">Type of items in the list.</typeparam>
    public void Shuffle<T>(IList<T> list)
    {
        for (int num = list.Count - 1; num > 0; num--)
        {
            int num2 = NextInt(num + 1);
            int index = num;
            int index2 = num2;
            T value = list[num2];
            T value2 = list[num];
            list[index] = value;
            list[index2] = value2;
        }
    }

    public SerializableRng ToSerializable()
    {
        SerializableRng serializableRng = new SerializableRng
        {
            counter = Counter,
        };
        _random.FillSerializableState(serializableRng);
        return serializableRng;
    }
}
