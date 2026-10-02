using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Wish : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState!;
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromCombatPile(
            combatState,
            Owner,
            Owner.PlayerCombatState!.DrawPile,
            minCount: 1,
            maxCount: 1,
            source: this);
        if (selected.FirstOrDefault() is { } card)
        {
            CardPileCmd.Add(card, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => AddKeywordInternal(CardKeyword.Retain);
}
