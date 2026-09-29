using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Whirlwind : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 5m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }
    protected override bool IsXEnergyCost => true;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int hitCount = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        await DamageCmd.Attack(_damage).WithHitCount(hitCount)
            .FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
    }

    protected override void OnUpgrade() => _damage += 3m;
}
