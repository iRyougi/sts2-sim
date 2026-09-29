using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Barrage : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 5m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = Owner.PlayerCombatState!.OrbQueue.Orbs.Count;
        return DamageCmd.Attack(_damage).WithHitCount(hits).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }
    protected override void OnUpgrade() => _damage += 2m;
}
