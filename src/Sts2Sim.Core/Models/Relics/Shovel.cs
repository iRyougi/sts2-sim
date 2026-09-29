using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Shovel : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool IsAllowed(IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override void ModifyAvailableRestSiteDecisions(IRunState runState, Player player, List<RestSiteDecision> decisions)
    {
        if (ReferenceEquals(player, Owner))
        {
            decisions.Add(new DigRestSiteOption());
        }
    }
}
