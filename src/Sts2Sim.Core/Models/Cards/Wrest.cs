using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Wrest : CardModel, ICardChoiceBaseValueProvider
{
    private int Cards => IsUpgraded ? 3 : 2;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override bool ShouldGlowGoldInternal
    {
        get
        {
            if (CombatState == null)
            {
                return false;
            }
            return CombatState.HittableEnemies.Any(c => ArrowCmd.CreatureIsImpaledByPlayersArrow(c, Owner));
        }
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (ArrowCmd.CreatureIsImpaledByPlayersArrow(cardPlay.Target, Owner))
        {
            while (ArrowCmd.CreatureIsImpaledByPlayersArrow(cardPlay.Target, Owner))
            {
                await ArrowCmd.DislodgeArrow(cardPlay.Target, cardPlay.Player, this);
            }

            await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        }
    }
}
