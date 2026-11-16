using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Uproot : CardModel, ICardChoiceBaseValueProvider
{
    private decimal Vulnerable => IsUpgraded ? 4m : 3m;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
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

            await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, Vulnerable, Owner.Creature, this);
        }
    }
}
