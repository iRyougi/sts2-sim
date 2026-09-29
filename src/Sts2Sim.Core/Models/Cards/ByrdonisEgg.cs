using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ByrdonisEgg : CardModel
{
    public override CardType Type => CardType.Quest;
    public override CardRarity Rarity => CardRarity.Quest;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    public override void ModifyAvailableRestSiteDecisions(
        IRunState runState,
        Player player,
        List<RestSiteDecision> decisions)
    {
        if (ReferenceEquals(player, Owner))
        {
            decisions.Add(new RestSiteDecision.Hatch());
        }
    }
}
