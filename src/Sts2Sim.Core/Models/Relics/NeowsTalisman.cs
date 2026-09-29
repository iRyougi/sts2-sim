using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class NeowsTalisman : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        List<CardModel> basics = Owner.Deck.Cards
            .Where(card => card.Rarity == CardRarity.Basic)
            .ToList();
        CardModel? strike = basics.LastOrDefault(card => card.Tags.Contains(CardTag.Strike));
        CardModel? defend = basics.LastOrDefault(card => card.Tags.Contains(CardTag.Defend));
        if (strike is not null)
        {
            CardCmd.Upgrade(strike);
        }
        if (defend is not null)
        {
            CardCmd.Upgrade(defend);
        }

        return Task.CompletedTask;
    }
}
