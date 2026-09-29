using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class StoneCracker : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom ||
            Owner.PlayerCombatState is not { } combatState)
        {
            return Task.CompletedTask;
        }

        List<CardModel> candidates =
            combatState.DrawPile.Cards.Where(card => card.IsUpgradable).ToList();
        candidates.StableShuffle(Owner.RunState.Rng.CombatCardSelection);
        foreach (CardModel card in candidates.Take(2))
        {
            card.Upgrade();
        }

        return Task.CompletedTask;
    }
}
