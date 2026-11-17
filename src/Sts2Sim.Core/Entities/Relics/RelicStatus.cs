namespace Sts2Sim.Core.Entities.Relics;

/// <summary>逐字移植 <c>MegaCrit.Sts2.Core.Entities.Relics.RelicStatus</c>（v0.113.1）：
/// 遗物在界面上的状态提示。<c>Active</c> = 下一次即将触发（图标脉冲），<c>Disabled</c> = 已用尽或失效。</summary>
public enum RelicStatus
{
    Normal,
    Active,
    Disabled,
}
