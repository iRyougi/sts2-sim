using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Rampage : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider, ICardComparisonBaseValueSource
{
    /// <summary>v0.113.1 CompareTo 视图：原版 <c>Damage</c> 的当前 BaseValue。</summary>
    public decimal GetComparisonBaseValue(string key) => key == "Damage"
        ? _damage
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Not a runtime-mutable comparison key.");

    private decimal _damage = 10m;
    private decimal _increase = 5m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        _damage += _increase;
    }

    protected override void OnUpgrade() => _increase += 5m;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_damage);
}
