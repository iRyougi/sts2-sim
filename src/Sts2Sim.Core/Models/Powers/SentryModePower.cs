using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>SentryModePower</c>：持有者每次回合抽牌前，把层数张 <see cref="SweepingGaze"/> 逐张生成到手牌。</summary>
public sealed class SentryModePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner.Player)
            return;

        for (int index = 0; index < Amount; index++)
        {
            var card = (SweepingGaze)ModelDb.Card<SweepingGaze>().MutableClone();
            card.AssignOwner(player);
            await CardPileCmd.Generate(Owner.CombatState!, card, PileType.Hand, player);
        }
    }
}
