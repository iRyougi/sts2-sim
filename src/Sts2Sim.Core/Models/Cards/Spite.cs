using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Spite : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 5m;
    private int _repeat = 2;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool lostHpThisTurn = CombatState!.DamageHistory.Entries.Any(entry =>
            entry.HappenedThisTurn(CombatState) &&
            ReferenceEquals(entry.Receiver, Owner.Creature) &&
            entry.Result.UnblockedDamage > 0m);
        await DamageCmd.Attack(_damage).WithHitCount(lostHpThisTurn ? _repeat : 1)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _repeat += 1;
}
