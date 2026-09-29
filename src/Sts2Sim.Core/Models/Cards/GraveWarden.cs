using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>GraveWarden</c>：获得 8（升级 11）格挡，把 1 张 <see cref="Soul"/> 洗进抽牌堆的随机位置。</summary>
public sealed class GraveWarden : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 1;

    private decimal Block => IsUpgraded ? 11m : 8m;

    private const int Cards = 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block, Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        var souls = new List<Soul>(Cards);
        for (int i = 0; i < Cards; i++)
        {
            var soul = (Soul)ModelDb.Card<Soul>().MutableClone();
            soul.AssignOwner(Owner);
            souls.Add(soul);
        }

        foreach (Soul soul in souls)
            await CardPileCmd.Generate(CombatState!, soul, PileType.Draw, Owner, CardPilePosition.Random);
    }
}
