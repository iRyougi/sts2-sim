using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.RestSite;

public sealed record KindleRestSiteOption : RestSiteDecision
{
    public override string OptionId => "kindle";
    public override int Priority => 4;

    public override Task ExecuteAsync(Player player)
    {
        player.Relics.OfType<PumpkinCandle>().FirstOrDefault()?.Rekindle();
        return Task.CompletedTask;
    }
}
