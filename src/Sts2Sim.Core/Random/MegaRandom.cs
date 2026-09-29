using System.Numerics;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Random;

/// <summary>
/// Xoshiro256** (xor, shift, rotate) pseudo-random number generator (PRNG).
/// Vendored verbatim from v0.109 game source (MegaCrit.Sts2.Core.Random.MegaRandom),
/// with deviations registered in Plan 02.5's deviation list:
///  - #26: NextBytes(Span&lt;byte&gt;) omitted (unsafe pointer path; no caller in the simulator).
///  - #21: Clone() added (not present in the game; needed for O(1) parallel env snapshots).
/// </summary>
public sealed class MegaRandom
{
    private const double _incrDouble = 1.1102230246251565E-16;

    private const float _incrFloat = 5.9604645E-08f;

    private ulong _s0;

    private ulong _s1;

    private ulong _s2;

    private ulong _s3;

    public MegaRandom(ulong seed)
    {
        Reinitialise(seed);
    }

    public MegaRandom(SerializableRng serializable)
    {
        Reinitialise(serializable);
    }

    private MegaRandom(ulong s0, ulong s1, ulong s2, ulong s3)
    {
        _s0 = s0;
        _s1 = s1;
        _s2 = s2;
        _s3 = s3;
    }

    /// <summary>
    /// Splitmix64 PRNG.
    /// </summary>
    public static ulong Splitmix64(ref ulong x)
    {
        ulong num = (x += 11400714819323198485uL);
        num = (num ^ (num >> 30)) * 13787848793156543929uL;
        num = (num ^ (num >> 27)) * 10723151780598845931uL;
        return num ^ (num >> 31);
    }

    public void Reinitialise(ulong seed)
    {
        _s0 = Splitmix64(ref seed);
        _s1 = Splitmix64(ref seed);
        _s2 = Splitmix64(ref seed);
        _s3 = Splitmix64(ref seed);
    }

    public void Reinitialise(SerializableRng serializable)
    {
        _s0 = serializable.state0;
        _s1 = serializable.state1;
        _s2 = serializable.state2;
        _s3 = serializable.state3;
    }

    private ulong NextULongInner()
    {
        ulong s = _s0;
        ulong s2 = _s1;
        ulong s3 = _s2;
        ulong s4 = _s3;
        ulong result = BitOperations.RotateLeft(s2 * 5, 7) * 9;
        ulong num = s2 << 17;
        s3 ^= s;
        s4 ^= s2;
        s2 ^= s3;
        s ^= s4;
        s3 ^= num;
        s4 = BitOperations.RotateLeft(s4, 45);
        _s0 = s;
        _s1 = s2;
        _s2 = s3;
        _s3 = s4;
        return result;
    }

    public int Next(int maxValue)
    {
        if (maxValue < 1)
        {
            throw new ArgumentOutOfRangeException("maxValue", maxValue, "maxValue must be > 0");
        }
        return NextInner(maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
        {
            throw new ArgumentOutOfRangeException("maxValue", maxValue, "maxValue must be > minValue");
        }
        long num = (long)maxValue - (long)minValue;
        if (num <= int.MaxValue)
        {
            return NextInner((int)num) + minValue;
        }
        return (int)(NextInner(num) + minValue);
    }

    public double NextDouble()
    {
        return (double)(NextULongInner() >> 11) * 1.1102230246251565E-16;
    }

    public int NextInt()
    {
        return (int)(NextULongInner() >> 33);
    }

    public uint NextUInt()
    {
        return (uint)NextULongInner();
    }

    public ulong NextULong()
    {
        return NextULongInner();
    }

    public bool NextBool()
    {
        return (NextULongInner() & 0x8000000000000000uL) != 0;
    }

    public float NextFloat()
    {
        return (float)(NextULongInner() >> 40) * 5.9604645E-08f;
    }

    private int NextInner(int maxValue)
    {
        return (int)(NextDouble() * (double)maxValue);
    }

    private long NextInner(long maxValue)
    {
        return (long)(NextDouble() * (double)maxValue);
    }

    public void FillSerializableState(SerializableRng rng)
    {
        rng.state0 = _s0;
        rng.state1 = _s1;
        rng.state2 = _s2;
        rng.state3 = _s3;
    }

    /// <summary>
    /// Deviation #21: exact, independent snapshot of the generator's internal state.
    /// Not present in the game; used for O(1) parallel environment resets.
    /// </summary>
    public MegaRandom Clone()
    {
        return new MegaRandom(_s0, _s1, _s2, _s3);
    }
}
