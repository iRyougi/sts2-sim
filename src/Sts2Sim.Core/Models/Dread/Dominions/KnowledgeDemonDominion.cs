using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class KnowledgeDemonDominion : DominionModel
{
    public override async Task BeforeHandDraw(Player player)
    {
        if (player.PlayerCombatState is not { TurnNumber: <= 1 } || player.Creature.IsDead)
        {
            return;
        }

        var sloth = (Sloth)ModelDb.Card<Sloth>().MutableClone();
        sloth.AssignOwner(player);
        var amnesia = (Amnesia)ModelDb.Card<Amnesia>().MutableClone();
        amnesia.AssignOwner(player);

        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            player.Creature.CombatState!, player, [sloth, amnesia], 1, 1, this);
        if (selected.Count > 0)
        {
            await ((KnowledgeDemon.IChoosable)selected[0]).OnChosen();
        }
    }
}
