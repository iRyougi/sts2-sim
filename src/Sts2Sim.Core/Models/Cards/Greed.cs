using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Greed : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    // sls2：原版 v0.111.0 只把它排除在修改器之外（CanBeGeneratedByModifiers），战斗中照常可以随机生成（如熵把诅咒变化成随机诅咒时的候选）。
    // 原来另把 CanBeGeneratedInCombat 设成 false，熵变化飞升者之祸时候选少三张，同一个随机数落到别的诅咒上（SIMZ0047：原版平庸、sts2-sim 受伤）
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Eternal, CardKeyword.Unplayable };
}
