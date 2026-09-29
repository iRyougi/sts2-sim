using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HeftyTablet : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        CardCreationOptions options = CardCreationOptions.ForNonCombatWithUniformOdds(
            [Owner.Character.CardPool], card => card.Rarity == CardRarity.Rare);
        IReadOnlyList<CardModel> candidates = CardFactory.CreateForReward(Owner, 3, options);
        CardModel? chosen = (await CardSelectCmd.SelectCardsAsync(
            Owner.RunState, Owner, candidates, 0, 1, this, cancelable: true)).FirstOrDefault();
        var injury = (Injury)ModelDb.Card<Injury>().MutableClone();
        injury.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(chosen is null ? [injury] : [chosen, injury]);
    }
}
