using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

public sealed class AeonglassDominionPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player.Creature != Owner)
        {
            return;
        }

        var wither = (Wither)ModelDb.Card<Wither>().MutableClone();
        wither.AssignOwner(player);
        await CardPileCmd.Generate(
            Owner.CombatState!,
            wither,
            PileType.Draw,
            creator: null,
            position: CardPilePosition.Top);
    }
}
