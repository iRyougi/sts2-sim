using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得 3（升级 4）点能量并消耗（原版 <c>TheEmperor</c>）。</summary>
public sealed class TheEmperor : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Energy => IsUpgraded ? 4 : 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(Energy, Owner);
    }
}
