using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Stoke : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel[] hand = Owner.PlayerCombatState!.Hand.Cards.ToArray();
        foreach (CardModel card in hand)
            await CardPileCmd.Exhaust(CombatState!, card);
        IReadOnlyList<CardModel> generated = CardFactory.GetForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState,
                Owner.RunState.Players.Count > 1),
            hand.Length,
            Owner.RunState.Rng.CombatCardGeneration);
        foreach (CardModel card in generated)
        {
            if (IsUpgraded) CardCmd.Upgrade(card);
            await CardPileCmd.Generate(CombatState!, card, PileType.Hand);
        }
    }
}
