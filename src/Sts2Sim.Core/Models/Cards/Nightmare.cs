using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Nightmare : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            CombatState!, Owner, Owner.PlayerCombatState!.Hand.Cards, 1, 1, this);
        if (selected.FirstOrDefault() is not { } card)
        {
            return;
        }

        NightmarePower? power = await PowerCmd.Apply<NightmarePower>(
            CombatState!, Owner.Creature, 3m, Owner.Creature, this);
        power?.SetSelectedCard(card);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
