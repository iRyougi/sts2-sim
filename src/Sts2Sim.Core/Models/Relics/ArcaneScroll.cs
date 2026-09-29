using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ArcaneScroll : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
        CardCreationOptions options = CardCreationOptions.ForNonCombatWithUniformOdds(
            [Owner.Character.CardPool], card => card.Rarity == CardRarity.Rare);
        CardModel card = CardFactory.CreateForReward(Owner, 1, options).Single();
        await CardPileCmd.AddToDeck(card);
    }
}
