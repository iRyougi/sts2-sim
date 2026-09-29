using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BladeDance : CardModel
{
    private int _cards = 3;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(CardPlay cardPlay) => await Shiv.CreateInHand(Owner, _cards, CombatState!);
    protected override void OnUpgrade() => _cards++;
}
