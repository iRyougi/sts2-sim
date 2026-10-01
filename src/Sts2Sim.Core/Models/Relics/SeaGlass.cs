using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SeaGlass : RelicModel
{
    private ModelId? _characterId;

    public ModelId? CharacterId
    {
        get => _characterId;
        set { AssertMutable(); _characterId = value; }
    }

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        CharacterId ??= ModelDb.Character<Ironclad>().Id;
        CharacterModel character = ModelDb.GetById<CharacterModel>(CharacterId);
        var cards = new List<CardModel>(15);
        foreach (CardRarity rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
        {
            CardCreationOptions options = CardCreationOptions.ForNonCombatWithUniformOdds(
                    [character.CardPool], card => card.Rarity == rarity)
                .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications);
            cards.AddRange(CardFactory.CreateForReward(Owner, 5, options));
        }

        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, cards, 0, cards.Count, this);
        foreach (CardModel card in selected)
            await CardPileCmd.AddToDeck(card);
    }
}
