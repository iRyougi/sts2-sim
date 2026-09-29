using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.RestSite;

public sealed record LiftRestSiteOption : RestSiteDecision
{
    public override string OptionId => "lift";
    public override int Priority => 4;

    public override Task ExecuteAsync(Player player)
    {
        player.Relics.OfType<Girya>().First().TimesLifted++;
        return Task.CompletedTask;
    }
}
