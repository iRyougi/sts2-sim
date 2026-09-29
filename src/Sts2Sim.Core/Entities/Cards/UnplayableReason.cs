namespace Sts2Sim.Core.Entities.Cards;

/// <summary>偏离 #33：枚举整体移植，但只有 EnergyCostTooHigh 分支当前可达。</summary>
[Flags]
public enum UnplayableReason
{
    None = 0,
    HasUnplayableKeyword = 2,
    BlockedByHook = 4,
    BlockedByCardLogic = 8,
    EnergyCostTooHigh = 0x10,
    StarCostTooHigh = 0x20,
    NoLivingAllies = 0x40,
}
