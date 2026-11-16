using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>多人专属：使队友接下来打出的若干张牌费用为 0（原版 <c>Ensemble</c>）。</summary>
public sealed class Ensemble : CardModel, ICardChoiceBaseValueProvider
{
    public override bool IsMultiplayerOnly => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Cards => IsUpgraded ? 5 : 4;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<EnsemblePower>(CombatState!, Owner.Creature, Cards, Owner.Creature, this);
    }
}
