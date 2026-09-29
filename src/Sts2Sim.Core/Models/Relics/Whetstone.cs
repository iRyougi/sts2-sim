using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Whetstone : RelicModel
{
    // 偏离 #148：上游 CanonicalVars 的 CardsVar(2)，以具名数量常量承载。
    private const int UpgradeCount = 2;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override bool HasUponPickupEffect => true;

    public override Task AfterObtained()
    {
        IEnumerable<CardModel> selected =
            Owner.Deck.Cards.Where(card => card != null && card.Type == CardType.Attack && card.IsUpgradable)
            .ToList().StableShuffle(Owner.RunState.Rng.Niche).Take(UpgradeCount);
        foreach (CardModel card in selected)
        {
            card.Upgrade();
        }

        return Task.CompletedTask;
    }
}
