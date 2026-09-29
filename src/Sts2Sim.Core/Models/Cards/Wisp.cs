using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Wisp</c>：0 费、消耗，获得 1 点能量；升级获得保留。</summary>
public sealed class Wisp : CardModel, ICardChoiceBaseValueProvider
{
    private const int Energy = 1;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override Task OnPlay(CardPlay cardPlay) => PlayerCmd.GainEnergy(Energy, Owner);

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
