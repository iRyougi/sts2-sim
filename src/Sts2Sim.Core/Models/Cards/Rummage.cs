using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>抽 3（升级 4）张牌并向手牌生成 1 张 Sadness（原版 <c>Rummage</c>）。</summary>
public sealed class Rummage : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private int Cards => IsUpgraded ? 4 : 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        var sadness = (CardModel)ModelDb.Card<Sadness>().MutableClone();
        sadness.AssignOwner(Owner);
        await CardPileCmd.Generate(CombatState!, sadness, PileType.Hand, Owner);
    }
}
