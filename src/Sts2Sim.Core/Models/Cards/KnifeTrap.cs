using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class KnifeTrap : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        CardModel[] shivs = Owner.PlayerCombatState!.ExhaustPile.Cards.Where(card => card.Tags.Contains(CardTag.Shiv)).ToArray();
        if (IsUpgraded)
        {
            foreach (CardModel shiv in shivs) CardCmd.Upgrade(shiv);
        }
        await AutoPlayCmd.FromCards(CombatState!, Owner, shivs, cardPlay.Target);
    }
}
