using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DistinguishedCape : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public async override Task AfterObtained()
    {
        List<CardModel> curses = CardPoolFilters.ForSinglePlayer(ModelDb.All<CardModel>())
            .Where(card => card.Rarity == CardRarity.Curse && card.CanBeGeneratedByModifiers)
            .OrderBy(card => card.Id.Entry, StringComparer.Ordinal)
            .ToList();
        for (int index = 0; index < 2 && curses.Count > 0; index++)
        {
            CardModel canonical = Owner.RunState.Rng.Niche.NextItem(curses)
                ?? throw new InvalidOperationException("No eligible curse is available for DistinguishedCape.");
            curses.Remove(canonical);
            var curse = (CardModel)canonical.MutableClone();
            curse.AssignOwner(Owner);
            await CardPileCmd.AddToDeck(curse);
        }

        for (int index = 0; index < 3; index++)
        {
            var apparition = (Apparition)ModelDb.Card<Apparition>().MutableClone();
            apparition.AssignOwner(Owner);
            await CardPileCmd.AddToDeck(apparition);
        }
    }
}
