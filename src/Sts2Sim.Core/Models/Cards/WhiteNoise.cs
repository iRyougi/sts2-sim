using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Cards;

public sealed class WhiteNoise : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? generated = CardFactory.GetDistinctForCombat(Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState,
                Owner.RunState.Players.Count > 1).Where(card => card.Type == CardType.Power),
            1, Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (generated is null) return;
        generated.SetToFreeThisTurn();
        await CardPileCmd.Generate(CombatState!, generated, PileType.Hand, Owner);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
