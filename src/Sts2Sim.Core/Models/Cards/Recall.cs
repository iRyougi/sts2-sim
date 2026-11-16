using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Recall : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (ArrowCmd.CreatureIsImpaledByPlayersArrow(cardPlay.Target, Owner))
        {
            while (ArrowCmd.CreatureIsImpaledByPlayersArrow(cardPlay.Target, Owner))
            {
                await ArrowCmd.DislodgeArrow(cardPlay.Target, cardPlay.Player, this);
            }

            CardModel? selected = (await CardSelectCmd.FromCombatPile(
                CombatState!,
                Owner,
                Owner.PlayerCombatState!.DiscardPile,
                1,
                1,
                this,
                cancelable: false)).FirstOrDefault();
            if (selected is not null)
            {
                CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
            }
        }
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
