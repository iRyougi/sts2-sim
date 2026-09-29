using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Acrobatics : CardModel
{
    private int _cards = 3;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
        await CardCmd.Discard(await CardSelectCmd.FromHandForDiscard(CombatState!, Owner, 1, this));
    }
    protected override void OnUpgrade() => _cards++;
}
