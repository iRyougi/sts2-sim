using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>抽3张,选1张放回堆顶。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Glimmer</c>）</summary>
public sealed class Glimmer : CardModel
{
    private int _draw = 3;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, _draw, Owner, fromHandDraw: false);
        CardModel? selected = (await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards, 1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade() => _draw += 1;
}
