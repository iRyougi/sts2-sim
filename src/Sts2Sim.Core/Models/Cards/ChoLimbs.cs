using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>本回合打出过 Wah 或 Ki 时减费 2，并获得两次格挡（原版 <c>ChoLimbs</c>）。</summary>
public sealed class ChoLimbs : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Cho];
    private decimal Block => IsUpgraded ? 7m : 5m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (!ReferenceEquals(card, this) || originalCost <= 0m)
            return false;
        if (!MelodyCmd.PlayedWahThisTurn(Owner) && !MelodyCmd.PlayedKiThisTurn(Owner))
            return false;

        modifiedCost = Math.Max(0m, originalCost - 2m);
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < 2; i++)
        {
            await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        }
    }
}
