using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Hologram : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 3m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);

    protected override async Task OnPlay(CardPlay play)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, play);
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            CombatState!, Owner, Owner.PlayerCombatState!.DiscardPile.Cards,
            1, 1, this)).FirstOrDefault();
        if (selected is not null)
            CardPileCmd.Add(selected, PileType.Hand);
    }

    protected override void OnUpgrade() { _block += 2m; RemoveKeyword(CardKeyword.Exhaust); }
}
