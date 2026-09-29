using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Pinpoint : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 15m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 3;
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (ReferenceEquals(card, this) && CloneOf is null)
            AddEnergyCostThisTurn(-(Owner.PlayerCombatState?.SkillCardsPlayedThisTurn ?? 0));
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && cardPlay.Card.Type == CardType.Skill)
            AddEnergyCostThisTurn(-1);
        return Task.CompletedTask;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
    protected override void OnUpgrade() => _damage += 4m;
}
