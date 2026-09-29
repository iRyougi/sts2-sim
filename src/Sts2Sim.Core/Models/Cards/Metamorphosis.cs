using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Metamorphosis : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IEnumerable<CardModel> attacks = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .Where(card => card.Type == CardType.Attack);
        foreach (CardModel generated in CardFactory.GetForCombat(
                     Owner, attacks, IsUpgraded ? 5 : 3, Owner.RunState.Rng.CombatCardGeneration))
        {
            generated.MakeFreeThisCombat();
            await CardPileCmd.Generate(CombatState!, generated, PileType.Draw, CardPilePosition.Random);
        }
    }
}
