using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>CollisionCourse/CrashLanding 生成的负面状态卡，逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Debris</c>）：
/// 1费，Exhaust，出牌无效果——纯粹占手牌位置。</summary>
public sealed class Debris : CardModel
{
    public override int MaxUpgradeLevel => 0;

    public override CardType Type => CardType.Status;

    public override CardRarity Rarity => CardRarity.Status;

    public override TargetType TargetType => TargetType.None;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override Task OnPlay(CardPlay cardPlay) => Task.CompletedTask;
}
