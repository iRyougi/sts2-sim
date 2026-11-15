namespace Sts2Sim.Core.Hooks;

[Flags]
public enum HpLossHookPhase
{
    None = 0,
    // Native BeforeWuppy is an explicit phase marker with no ModifyHpLost callbacks.
    BeforeWuppy = 1,
    BeforeOsty = 2,
    AfterOsty = 4,
    All = 6,
}
