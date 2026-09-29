using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat.StateDescription;

public struct CombatStateDescriptionBuilder
{
    private const ulong OffsetA = 14695981039346656037UL;
    private const ulong OffsetB = 1099511628211UL;
    private ulong _a;
    private ulong _b;

    public CombatStateDescriptionBuilder()
    {
        _a = OffsetA;
        _b = 0x9E3779B97F4A7C15UL;
    }

    public void Append(bool value) => Append(value ? 1UL : 0UL);

    public void Append(int value) => Append(unchecked((ulong)(long)value));

    public void Append(uint? value)
    {
        Append(value.HasValue);
        if (value.HasValue)
        {
            Append((ulong)value.Value);
        }
    }

    public void Append(decimal value)
    {
        foreach (int part in decimal.GetBits(value))
        {
            Append(part);
        }
    }

    public void Append(ModelId value)
    {
        Append(value.Category);
        Append(value.Entry);
    }

    public void Append(string? value)
    {
        if (value is null)
        {
            Append(ulong.MaxValue);
            return;
        }

        Append(value.Length);
        foreach (char character in value)
        {
            Append(character);
        }
    }

    public void Append(ulong value)
    {
        unchecked
        {
            _a ^= value;
            _a *= 1099511628211UL;
            _b ^= value + 0x9E3779B97F4A7C15UL + (_b << 6) + (_b >> 2);
            _b = (_b << 27) | (_b >> 37);
            _b *= 0x94D049BB133111EBUL;
        }
    }

    public CombatStateDescriptionDigest Build() => new(_a, _b);
}
