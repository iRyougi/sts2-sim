using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MeatCleaver : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override void ModifyAvailableRestSiteDecisions(
        IRunState runState,
        Player player,
        List<RestSiteDecision> decisions)
    {
        if (ReferenceEquals(player, Owner) && player.Deck.Cards.Count(card => card.IsRemovable) >= 2)
        {
            decisions.Add(new RestSiteDecision.Cook());
        }
    }
}
