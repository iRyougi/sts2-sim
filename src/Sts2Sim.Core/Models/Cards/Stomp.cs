using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Stomp : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 12m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute();
    }

    protected override void OnUpgrade() => _damage += 3m;

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (ReferenceEquals(card, this) && CloneOf is null)
            AddEnergyCostThisTurn(-Owner.PlayerCombatState!.AttackCardsPlayedThisTurn);
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner, Owner) && cardPlay.Card.Type == CardType.Attack)
            AddEnergyCostThisTurn(-1);
        return Task.CompletedTask;
    }
}
