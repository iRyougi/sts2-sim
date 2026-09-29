using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class HelixDrill : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 3m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }

    protected override async Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        var state = (CombatState)CombatState!;
        int hits = state.SemanticHistory.SumEnergySpentThisTurn(state, Owner);
        if (Pile?.Type == PileType.Play)
            hits -= EnergyCost;
        await DamageCmd.Attack(_damage).WithHitCount(hits).FromCard(this, play)
            .Targeting(play.Target).Execute();
    }

    protected override void OnUpgrade() => _damage += 2m;
}
