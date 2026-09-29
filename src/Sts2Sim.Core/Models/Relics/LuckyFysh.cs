using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LuckyFysh : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override bool IsAllowedInShops => false;

    public override Task AfterCardChangedPiles(
        CardModel card,
        PileType oldPileType,
        AbstractModel? clonedBy)
    {
        _ = clonedBy;
        if (card.Owner != Owner ||
            card.Pile?.Type != PileType.Deck ||
            oldPileType == PileType.Deck)
        {
            return Task.CompletedTask;
        }

        return PlayerCmd.GainGold(15m, Owner);
    }
}
