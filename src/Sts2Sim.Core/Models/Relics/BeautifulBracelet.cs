using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BeautifulBracelet : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public async override Task AfterObtained()
    {
        using IDisposable rngScope = Owner.PlayerRng.BeginSemanticScope(
            $"{Owner.CurrentSemanticLocationKey}/noncombat/BeautifulBracelet");
        Swift swift = ModelDb.GetById<Swift>(ModelDb.GetId<Swift>());
        // Native TakeRandom materializes the eligible deck order, shuffles the whole list, then takes four.
        IEnumerable<CardModel> selected = Owner.Deck.Cards.Where(swift.CanEnchant).ToList()
            .UnstableShuffle(Owner.PlayerRng.NonCombatGeneration).Take(4);
        foreach (CardModel card in selected)
        {
            await CardCmd.Enchant<Swift>(card, 2m);
        }
    }
}
