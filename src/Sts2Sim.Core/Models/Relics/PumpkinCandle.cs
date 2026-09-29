using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PumpkinCandle : RelicModel
{
    public int KindleCount { get; private set; }
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task AfterObtained() { Rekindle(); return Task.CompletedTask; }
    public void Rekindle() { AssertMutable(); KindleCount += 5; }
    public override void ModifyAvailableRestSiteDecisions(IRunState runState, Player player, List<RestSiteDecision> decisions)
    {
        if (ReferenceEquals(player, Owner))
        {
            decisions.Add(new KindleRestSiteOption());
        }
    }
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner && KindleCount > 0 ? amount + 1m : amount;
    public override Task AfterCombatEnd() { if (KindleCount > 0) KindleCount--; return Task.CompletedTask; }
    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(KindleCount);
}
