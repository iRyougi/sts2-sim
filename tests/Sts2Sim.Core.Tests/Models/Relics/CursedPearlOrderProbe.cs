using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Relics;

internal sealed class CursedPearlOrderProbe : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public int? GoldWhenGreedEnteredDeck { get; private set; }

    public override Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        if (card is Greed && ReferenceEquals(card.Owner, Owner))
        {
            GoldWhenGreedEnteredDeck = Owner.Gold;
        }

        return Task.CompletedTask;
    }
}
