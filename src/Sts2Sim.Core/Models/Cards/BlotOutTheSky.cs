using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>生成 1 张随机 Shoot 牌入手牌并本回合免费；消耗（原版 <c>BlotOutTheSky</c>）。</summary>
public sealed class BlotOutTheSky : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        bool isMultiplayer = Owner.RunState.Players.Count > 1;
        CardModel? card = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, isMultiplayer)
                .Where(c => c.Tags.Contains(CardTag.Shoot)),
            1,
            Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (card is not null)
        {
            card.SetToFreeThisTurn();
            await CardPileCmd.Generate(CombatState!, card, PileType.Hand, Owner);
        }
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
