using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class HandTrick : CardModel
{
    public override bool GainsBlock => true;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, IsUpgraded ? 10m : 7m, ValueProp.Move, this, cardPlay);
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards.Where(card => card.Type == CardType.Skill && !card.HasKeyword(CardKeyword.Sly)),
            1, 1, this);
        if (selected.FirstOrDefault() is { } skill)
        {
            CardCmd.ApplySingleTurnSly(skill);
        }
    }
}
