namespace Sts2Sim.Core.Hooks;

[Flags]
public enum ModifyDamageHookType
{
    None = 0,
    Additive = 2,
    Multiplicative = 4,
    Cap = 8,
    All = 0xE,
}
