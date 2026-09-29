using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Models.Enchantments;

public sealed class SlitherXCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
}
