using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Seance</c>：1 费（升级 0 费）、虚无，从抽牌堆选 1 张牌变化为 <see cref="Soul"/>。</summary>
public sealed class Seance : CardModel, ICardChoiceBaseValueProvider
{
    private const int CardCount = 1;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: CardCount);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> selection = await CardSelectCmd.SelectCardsAsync(CombatState!, Owner,
            Owner.PlayerCombatState!.DrawPile.Cards, CardCount, CardCount, this);
        foreach (CardModel card in selection.ToList())
        {
            await CardCmd.TransformTo<Soul>(card, Owner.RunState);
        }
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
