using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Models;

internal sealed class NegativeCostCard : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    protected override int CanonicalEnergyCost => -1;
}
