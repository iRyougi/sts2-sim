namespace Sts2Sim.Core.Entities.FoolArrows;

/// <summary>允许 HeavyShot 等射击牌按实例调整 Aim 加成量而不让 G1 反向依赖 G2。</summary>
internal interface IAimAmountModifier
{
    decimal ModifyAimAmount(decimal amount);
}
