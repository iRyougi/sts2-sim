using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.RestSite;

public sealed record DigRestSiteOption : RestSiteDecision
{
    public override string OptionId => "dig";
    public override int Priority => 4;

    public override Task ExecuteAsync(Player player) =>
        RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(player), player);
}
