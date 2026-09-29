using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Tactician : CardModel
{
    private int _energy = 1;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
    protected override Task OnPlay(CardPlay cardPlay) => PlayerCmd.GainEnergy(_energy, Owner);
    protected override void OnUpgrade() => _energy++;
}
