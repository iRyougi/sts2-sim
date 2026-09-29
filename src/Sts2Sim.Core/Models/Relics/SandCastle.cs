using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SandCastle : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;
    public override Task AfterObtained()
    {
        List<CardModel> cards = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        cards.StableShuffle(Owner.RunState.Rng.Niche);
        foreach (CardModel card in cards.Take(6)) CardCmd.Upgrade(card);
        return Task.CompletedTask;
    }
}
