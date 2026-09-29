using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Slimed : CardModel
{
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay) =>
        await CardPileCmd.Draw(CombatState!, 1, Owner, fromHandDraw: false);
}
