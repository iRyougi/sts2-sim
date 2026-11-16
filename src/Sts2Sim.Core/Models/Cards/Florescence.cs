using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>消耗全部非消耗堆 Shoot 牌并生成按消耗数强化的 TheShot（原版 <c>Florescence</c>）。</summary>
public sealed class Florescence : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<CardModel> shootCards = Owner.PlayerCombatState!.AllPiles
            .Where(pile => pile.Type != PileType.Exhaust)
            .SelectMany(pile => pile.Cards)
            .Where(card => card.Tags.Contains(CardTag.Shoot))
            .ToList();
        int count = shootCards.Count;
        foreach (CardModel shootCard in shootCards)
        {
            await CardPileCmd.Exhaust(CombatState!, shootCard);
        }

        var theShot = (TheShot)ModelDb.Card<TheShot>().MutableClone();
        theShot.AssignOwner(Owner);
        if (IsUpgraded)
        {
            theShot.Upgrade();
        }
        theShot.BuffFromFlorescence(count);
        await CardPileCmd.Generate(CombatState!, theShot, PileType.Hand);
    }
}
