namespace Sts2Sim.Core.Entities.Cards;

/// <summary>
/// 逐字移植（<c>MegaCrit.Sts2.Core.Entities.Cards.KeywordSources</c>）。Local 是卡牌自身保存的关键字（规范关键字加减
/// <c>AddKeyword</c> / <c>RemoveKeyword</c>）；Global 由战斗中其他模型按需给出、从不写回卡牌，例如 HexPower 给
/// Hexed 卡的 Ethereal。
/// </summary>
[Flags]
public enum KeywordSources
{
    None = 0,
    Local = 2,
    Global = 4,
    All = -1,
}
