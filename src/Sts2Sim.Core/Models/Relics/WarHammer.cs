using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class WarHammer : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task AfterCombatVictory()
    {
        if (Owner.RunState.CurrentRoom is not CombatRoom { RoomType: RoomType.Elite }) return Task.CompletedTask;
        List<CardModel> cards = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        cards.StableShuffle(Owner.RunState.Rng.Niche);
        foreach (CardModel card in cards.Take(4)) CardCmd.Upgrade(card);
        return Task.CompletedTask;
    }
}
