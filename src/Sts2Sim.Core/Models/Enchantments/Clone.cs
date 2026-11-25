using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Enchantments;

/// <summary>v0.114.0 (#1110): the enchantment itself offers the Clone rest-site option (it lived on Pael's Growth in
/// v0.113.1). It is here rather than on the relic because the enchanted card can be removed.
/// Adapted from native <c>TryModifyRestSiteOptions</c> to the simulator's <c>ModifyAvailableRestSiteDecisions</c>.</summary>
public sealed class Clone : EnchantmentModel
{
    public override void ModifyAvailableRestSiteDecisions(
        IRunState runState, Player player, List<RestSiteDecision> decisions)
    {
        if (!ReferenceEquals(player, Owner.Owner))
        {
            return;
        }

        if (decisions.OfType<CloneRestSiteOption>().Any())
        {
            return;
        }

        decisions.Add(new CloneRestSiteOption(player));
    }
}
