using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>向手牌依次生成 2 张 Sadness 与 2（升级 3）张 Joy 并消耗（原版 <c>BrightenUp</c>）。</summary>
public sealed class BrightenUp : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int JoyCount => IsUpgraded ? 3 : 2;
    private const int SadnessCount = 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < SadnessCount; i++)
        {
            var sadness = (CardModel)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, sadness, PileType.Hand, Owner);
        }

        for (int i = 0; i < JoyCount; i++)
        {
            var joy = (CardModel)ModelDb.Card<Joy>().MutableClone();
            joy.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, joy, PileType.Hand, Owner);
        }
    }
}
