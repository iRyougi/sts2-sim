using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得 14（升级 18）点格挡并向手牌生成 1 张 Sadness（原版 <c>Jest</c>）。</summary>
public sealed class Jest : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    private decimal Block => IsUpgraded ? 18m : 14m;
    private const int Cards = 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block, Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        for (int i = 0; i < Cards; i++)
        {
            var sadness = (CardModel)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, sadness, PileType.Hand, Owner);
        }
    }
}
