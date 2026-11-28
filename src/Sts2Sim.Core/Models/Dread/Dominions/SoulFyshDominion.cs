using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class SoulFyshDominion : DominionModel
{
    protected override async Task AfterPlayersEnteredCombat(IReadOnlyList<Player> players)
    {
        AssertMutable();
        foreach (Player player in players)
        {
            var beckon = (Beckon)ModelDb.Card<Beckon>().MutableClone();
            beckon.AssignOwner(player);
            await CardPileCmd.Generate(
                player.Creature.CombatState!,
                beckon,
                PileType.Draw,
                creator: player,
                position: CardPilePosition.Random);
        }
    }
}
