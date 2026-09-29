using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PandorasBox : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        CardModel[] originals = Owner.Deck.Cards.Where(card => card.Rarity == CardRarity.Basic &&
            (card.Tags.Contains(CardTag.Strike) || card.Tags.Contains(CardTag.Defend)) && card.IsRemovable).ToArray();
        IEnumerable<CardTransformation> transformations = originals.Select(original =>
            new CardTransformation(original, CardFactory.CreateRandomCardForTransform(original, false, Owner.RunState.Rng.Niche)));
        await CardCmd.Transform(transformations, null);
    }
}
