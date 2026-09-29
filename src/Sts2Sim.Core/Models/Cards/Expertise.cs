using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Expertise: retain only the cards actually drawn by this play.</summary>
public sealed class Expertise : CardModel
{
    private int _cards = 2;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel card in await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false))
        {
            CardCmd.ApplySingleTurnRetain(card);
        }
    }
    protected override void OnUpgrade() => _cards++;
}
