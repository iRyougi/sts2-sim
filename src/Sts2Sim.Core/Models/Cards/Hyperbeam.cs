using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Hyperbeam : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 24m;
    private readonly decimal _focusLoss = 3m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }

    protected override async Task OnPlay(CardPlay play)
    {
        await DamageCmd.Attack(_damage).FromCard(this, play)
            .TargetingAllOpponents(CombatState!).Execute();
        await PowerCmd.Apply<HyperbeamFocusDownPower>(CombatState!, Owner.Creature,
            _focusLoss, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _damage += 6m;
}
