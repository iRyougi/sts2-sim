using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Prepared : CardModel
{
    private int _cards = 1;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
        await CardCmd.Discard(await CardSelectCmd.FromHandForDiscard(CombatState!, Owner, _cards, this));
    }
    protected override void OnUpgrade() => _cards++;
}
