using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Apotheosis : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Innate];

    protected override Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel card in Owner.PlayerCombatState!.AllPiles
                     .SelectMany(pile => pile.Cards)
                     .Where(card => card != this && card.IsUpgradable))
        {
            CardCmd.Upgrade(card);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
