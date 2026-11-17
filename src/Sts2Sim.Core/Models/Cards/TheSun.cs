using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>抽 2（升级 3）张牌、获得 2（升级 3）点能量并将 1 张 Soot 置入弃牌堆（原版 <c>TheSun</c>）。</summary>
public sealed class TheSun : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Amount => IsUpgraded ? 3 : 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Amount, Energy: Amount);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, Amount, Owner, fromHandDraw: false);
        await PlayerCmd.GainEnergy(Amount, Owner);
        var soot = (CardModel)ModelDb.Card<Soot>().MutableClone();
        soot.AssignOwner(Owner);
        await CardPileCmd.Generate(CombatState!, soot, PileType.Discard, Owner);
    }
}
