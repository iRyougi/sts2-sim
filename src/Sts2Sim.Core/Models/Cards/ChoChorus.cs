using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得格挡并自动打出手中全部 Wah 与 Ki 牌（原版 <c>ChoChorus</c>）。</summary>
public sealed class ChoChorus : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Cho];
    private decimal Block => IsUpgraded ? 18m : 14m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        List<CardModel> toPlay = Owner.PlayerCombatState!.Hand.Cards
            .Where(c => c.Tags.Contains(CardTag.Wah) || c.Tags.Contains(CardTag.Ki))
            .ToList();
        foreach (CardModel card in toPlay)
        {
            await AutoPlayCmd.FromCards(CombatState!, Owner, [card]);
        }
    }
}
