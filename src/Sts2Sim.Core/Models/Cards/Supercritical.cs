using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Supercritical : CardModel, ICardChoiceBaseValueProvider
{
    private int _energy = 4;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: _energy);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override Task OnPlay(CardPlay cardPlay) => PlayerCmd.GainEnergy(_energy, Owner);
    protected override void OnUpgrade() => _energy += 2;
}
