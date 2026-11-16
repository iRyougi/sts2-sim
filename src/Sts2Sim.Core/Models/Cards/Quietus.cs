using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Quietus : CardModel, ICardChoiceBaseValueProvider
{
    private decimal QuietusAmount => IsUpgraded ? 3m : 2m;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<QuietusPower>(CombatState!, Owner.Creature, QuietusAmount, Owner.Creature, this);
}
