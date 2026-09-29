using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Demesne</c>：3 费（升级 2）虚无能力，施加 1 层 <see cref="DemesnePower"/>（每回合多抽 1、最大能量 +1）。</summary>
public sealed class Demesne : CardModel, ICardChoiceBaseValueProvider
{
    private const int Energy = 1;
    private const int Cards = 1;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 3;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards, Energy: Energy);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<DemesnePower>(CombatState!, Owner.Creature, Cards, Owner.Creature, this);

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
