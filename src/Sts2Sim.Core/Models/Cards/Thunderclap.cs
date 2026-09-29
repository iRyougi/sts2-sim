using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Thunderclap : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 4m;
    private decimal _vulnerable = 1m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
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
        foreach (var enemy in CombatState!.HittableEnemies.ToArray())
            await PowerCmd.Apply<VulnerablePower>(CombatState, enemy, _vulnerable,
                Owner.Creature, this);
    }

    protected override void OnUpgrade() => _damage += 3m;
}
