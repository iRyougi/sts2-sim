using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Entities.Merchant;

/// <summary>逐字移植 <c>MegaCrit.Sts2.Core.Entities.Merchant.MerchantCardRemovalEntry</c> 的定价。
/// <para>
/// 真实基础移除价格通胀前为 75 金币、每次涨价 25 金币；通胀后为 100 金币、每次涨价 50 金币。
/// 本仓库此前只硬编码了 A6+ 的 100/50，A0 因此偏高——Plan 08b-3g 修复。
/// </para></summary>
public sealed class MerchantCardRemovalEntry : MerchantEntry
{
    public MerchantCardRemovalEntry(int price, Player? player = null)
        : base(price, player)
    {
    }

    public static int PriceFor(AscensionManager ascension, int removalsUsed) =>
        ascension.GetValueIfAscension(AscensionLevel.Inflation, 100, 75)
        + ascension.GetValueIfAscension(AscensionLevel.Inflation, 50, 25) * removalsUsed;
}
