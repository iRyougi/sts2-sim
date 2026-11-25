using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>随机自动打出手牌中两张（升级三张）可用牌；没有可用牌时不打出（v0.114.0 原版 <c>TheFool</c>）。</summary>
public sealed class TheFool : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Cards => IsUpgraded ? 3 : 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < Cards; i++)
        {
            CardModel? picked = Owner.PlayerCombatState!.Hand.Cards
                .Where(c => !c.Keywords.Contains(CardKeyword.Unplayable))
                .ToList()
                .StableShuffle(Owner.RunState.Rng.CombatCardSelection)
                .FirstOrDefault();
            if (picked is not null && !CombatState!.IsOverOrEnding() && !picked.Owner.Creature.IsDead)
            {
                await AutoPlayCmd.FromCards(CombatState!, Owner, [picked]);
            }
        }
    }
}
