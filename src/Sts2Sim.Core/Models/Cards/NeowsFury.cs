using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Deviation #158: retrieves the first eligible discard cards because the simulator has no
/// interactive discard-card selection UI.
/// </summary>
public sealed class NeowsFury : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 10m;
    private int _retrieveCount = 2;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();

        int availableCapacity = Math.Max(
            0,
            CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count);
        CardModel[] selected = Owner.PlayerCombatState.DiscardPile.Cards
            .Take(Math.Min(_retrieveCount, availableCapacity))
            .ToArray();
        foreach (CardModel card in selected)
        {
            CardPileCmd.Add(card, PileType.Hand);
        }
    }

    protected override void OnUpgrade()
    {
        _damage = 14m;
        _retrieveCount = 3;
    }
}
