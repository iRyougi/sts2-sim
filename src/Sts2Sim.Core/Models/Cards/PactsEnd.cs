using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class PactsEnd : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage, Cards: 3);

    private decimal _damage = 18m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 0;
    protected override Task OnPlay(CardPlay cardPlay) =>
        Owner.PlayerCombatState!.ExhaustPile.Cards.Count >= 3
            ? DamageCmd.Attack(_damage).FromCard(this, cardPlay)
                .TargetingAllOpponents(CombatState!).Execute()
            : Task.CompletedTask;
    protected override void OnUpgrade() => _damage += 6m;
}
