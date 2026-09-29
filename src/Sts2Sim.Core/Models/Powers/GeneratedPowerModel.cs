using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// Stackable state carrier for Plan 06b powers. 复杂行为边界：除已专门实现的 Vigor、
/// Seeking Edge 与 Parry 交互外，复杂 Power 的回合/出牌 hook 目前仅保存层数。
/// </summary>
public abstract class GeneratedPowerModel : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;
}
