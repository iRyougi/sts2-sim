using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Refract : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 10m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).WithHitCount(2).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
        for (int i = 0; i < 2; i++)
            await OrbCmd.Channel<GlassOrb>(CombatState!, Owner);
    }

    protected override void OnUpgrade() => _damage += 3m;
}
