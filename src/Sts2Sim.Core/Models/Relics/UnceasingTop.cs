using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class UnceasingTop : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterHandEmptied(Player player)
    {
        if (player == Owner && IsValidPhase(player.PlayerCombatState!.Phase))
        {
            await CardPileCmd.Draw(Owner.Creature.CombatState!, 1, Owner, fromHandDraw: false);
        }
    }

    /// <summary>Verbatim port of the game's <c>UnceasingTop.IsValidPhase</c>: <c>(uint)(phase - 2) &lt;= 2u</c>, i.e.
    /// AutoPrePlay, Play and AutoPostPlay. Don't trigger before hand draw (Start) or after hand flush (End): a card
    /// autoplayed there would find the hand always empty, so the relic would always draw.</summary>
    private static bool IsValidPhase(PlayerTurnPhase phase) =>
        phase is PlayerTurnPhase.AutoPrePlay or PlayerTurnPhase.Play or PlayerTurnPhase.AutoPostPlay;
}
