using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Enlightenment : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel card in Owner.PlayerCombatState!.Hand.Cards.Where(card => card.EnergyCost > 1))
        {
            if (IsUpgraded) card.SetTemporaryCostOverrideThisCombat(1);
            else card.SetTemporaryCostOverrideThisTurnOrUntilPlayed(1);
        }
        return Task.CompletedTask;
    }
}
