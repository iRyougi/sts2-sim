using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>从其他角色池随机生成卡牌填满手牌；升级时同时升级生成牌（原版 <c>TheLovers</c>）。</summary>
public sealed class TheLovers : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<CardPoolModel> pools = Owner.UnlockState.CharacterCardPools.ToList();
        if (pools.Count > 1)
        {
            pools.Remove(Owner.Character.CardPool);
        }

        bool isMultiplayer = Owner.RunState.Players.Count > 1;
        IEnumerable<CardModel> poolCards = pools.SelectMany(p => p.GetUnlockedCards(Owner.UnlockState, isMultiplayer));
        int count = Math.Max(0, CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count);
        IReadOnlyList<CardModel> generated = CardFactory.GetForCombat(
            Owner,
            poolCards,
            count,
            Owner.RunState.Rng.CombatCardGeneration);
        foreach (CardModel card in generated)
        {
            if (IsUpgraded && card.IsUpgradable)
            {
                card.Upgrade();
            }

            await CardPileCmd.Generate(CombatState!, card, PileType.Hand, Owner);
        }
    }
}
