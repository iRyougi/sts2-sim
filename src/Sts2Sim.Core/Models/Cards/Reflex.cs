using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Reflex : CardModel
{
    private int _cards = 2;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
    protected override async Task OnPlay(CardPlay cardPlay) => await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
    protected override void OnUpgrade() => _cards++;
}
