using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DarkstonePeriapt : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        if (card.Owner == Owner && card.Pile?.Type == PileType.Deck && card.Type == CardType.Curse)
            await CreatureCmd.GainMaxHp(Owner.Creature, 6m);
    }
}
