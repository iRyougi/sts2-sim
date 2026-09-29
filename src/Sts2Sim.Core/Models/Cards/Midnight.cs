using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Midnight : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    private decimal _damage = 60m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 12;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        return DamageCmd.Attack(_damage).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (ReferenceEquals(card, this) && CloneOf is null)
        {
            AddEnergyCostThisCombat(
                -(CombatState is CombatState concrete ? concrete.CardsExhaustedThisCombat : 0));
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        AddEnergyCostThisCombat(-1);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => _damage += 12m;
}
