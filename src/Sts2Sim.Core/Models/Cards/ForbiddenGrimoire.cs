using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>ForbiddenGrimoire</c>（先古）：永恒，2（升级 1）费能力，获得 1 层
/// <see cref="ForbiddenGrimoirePower"/>（战斗结束时每层追加一个删牌奖励）。</summary>
public sealed class ForbiddenGrimoire : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Ancient;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Eternal];

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<ForbiddenGrimoirePower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
