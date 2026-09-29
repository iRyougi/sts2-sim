using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Parse</c>：虚无，抽 3（升级 4）。</summary>
public sealed class Parse : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    private int Cards => IsUpgraded ? 4 : 3;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override Task OnPlay(CardPlay cardPlay) =>
        CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
}
