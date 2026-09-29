using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Adrenaline: gain energy before drawing, then Exhaust.</summary>
public sealed class Adrenaline : CardModel
{
    private int _energy = 1;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(_energy, Owner);
        await CardPileCmd.Draw(CombatState!, 2, Owner, fromHandDraw: false);
    }
    protected override void OnUpgrade() => _energy++;
}
