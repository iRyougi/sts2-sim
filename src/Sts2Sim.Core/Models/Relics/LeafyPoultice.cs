using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LeafyPoultice : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
        await CreatureCmd.LoseMaxHp(Owner.RunState, Owner.Creature, 12m, isFromCard: false);
        List<CardModel> basics = Owner.Deck.Cards
            .Where(card => card.Rarity == CardRarity.Basic)
            .ToList();
        CardModel? strike = basics.FirstOrDefault(card => card.Tags.Contains(CardTag.Strike));
        CardModel? defend = basics.FirstOrDefault(card => card.Tags.Contains(CardTag.Defend));
        var transformations = new List<CardTransformation>();
        if (strike is not null)
        {
            transformations.Add(new CardTransformation(strike));
        }
        if (defend is not null)
        {
            transformations.Add(new CardTransformation(defend));
        }
        await CardCmd.Transform(transformations, Owner.PlayerRng.Transformations);
    }
}
