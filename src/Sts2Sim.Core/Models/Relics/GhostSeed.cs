using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GhostSeed : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is not CombatRoom || Owner.PlayerCombatState is null)
        {
            return Task.CompletedTask;
        }

        foreach (CardModel card in Owner.PlayerCombatState.AllPiles.SelectMany(pile => pile.Cards))
        {
            if (card.Owner == Owner &&
                card.Rarity == CardRarity.Basic &&
                (card.Tags.Contains(CardTag.Strike) || card.Tags.Contains(CardTag.Defend)))
            {
                card.AddKeywordInternal(CardKeyword.Ethereal);
            }
        }

        return Task.CompletedTask;
    }
}
