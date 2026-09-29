using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>封印王座，自己每打出一张卡（结算前）获得等于层数的星愿。
/// 逐字移植（<c>MegaCrit.Sts2.Core.Models.Powers.TheSealedThronePower</c>）。</summary>
public sealed class TheSealedThronePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player)
        {
            await PlayerCmd.GainStars(Amount, Owner.Player!);
        }
    }
}
