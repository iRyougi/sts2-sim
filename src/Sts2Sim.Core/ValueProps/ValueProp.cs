namespace Sts2Sim.Core.ValueProps;

/// <summary>
/// 伤害类型标记：Unblockable = 类似中毒的直接掉血；Unpowered = 来自遗物/药水/power 的伤害；
/// Move = 来自攻击卡/怪物攻击的伤害。逐字移植自 v0.109。
/// </summary>
[Flags]
public enum ValueProp
{
    Unblockable = 2,
    Unpowered = 4,
    Move = 8,
    SkipHurtAnim = 0x10,
}

public static class ValuePropExtensions
{
    /// <summary>Whether this is attack damage from a powered card or monster move.</summary>
    public static bool IsPoweredAttack(this ValueProp props) =>
        props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);

    /// <summary>Whether this is block from a powered card or monster move.</summary>
    public static bool IsPoweredCardOrMonsterMoveBlock(this ValueProp props) =>
        props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
}
