using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>生成 3（升级 4）张不同无色牌入手牌（v0.114.0 删除了减费）（原版 <c>TheHierophant</c>）。</summary>
public sealed class TheHierophant : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Cards => IsUpgraded ? 4 : 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> generated = CardFactory.GetDistinctForCombat(
            Owner,
            ColorlessCardPool.Instance.GetUnlockedCards(Owner.UnlockState, isMultiplayer: Owner.RunState.Players.Count > 1),
            Cards,
            Owner.RunState.Rng.CombatCardGeneration);
        foreach (CardModel card in generated)
        {
            await CardPileCmd.Generate(CombatState!, card, PileType.Hand, Owner);
        }
    }
}
