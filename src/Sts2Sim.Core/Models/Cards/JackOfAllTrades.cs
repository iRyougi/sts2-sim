using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

public sealed class JackOfAllTrades : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState ?? throw new InvalidOperationException("Jack of All Trades requires combat.");
        var candidates = ColorlessCardPool.Instance.GetUnlockedCards(
                Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .Where(card => card is not JackOfAllTrades)
            .ToList();

        int count = IsUpgraded ? 2 : 1;
        foreach (CardModel generated in CardFactory.GetDistinctForCombat(
            Owner, candidates, count, combatState.RunState.Rng.CombatCardGeneration))
        {
            await CardPileCmd.Generate(combatState, generated, PileType.Hand);
        }
    }
}
