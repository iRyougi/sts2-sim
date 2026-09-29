using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class DualWield : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards.Where(card => card.Type is CardType.Attack or CardType.Power),
            1, 1, this);
        CardModel? original = selected.FirstOrDefault();
        if (original is null) return;
        for (int i = 0; i < (IsUpgraded ? 2 : 1); i++)
            await CardPileCmd.Generate(CombatState!, original.CreateClone(), PileType.Hand, Owner);
    }
}
