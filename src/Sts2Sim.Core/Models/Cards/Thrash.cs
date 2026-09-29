using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Thrash : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 4m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues =>
        new CardChoiceBaseValues(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).WithHitCount(2).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
        CardModel? selected = Owner.RunState.Rng.CombatCardSelection.NextItem(
            Owner.PlayerCombatState!.Hand.Cards.Where(card => card.Type == CardType.Attack));
        if (selected is null) return;
        decimal damage = selected is ICardDamageVariableProvider variable &&
            variable.TryGetThrashDamageVariable(out decimal baseValue)
            ? baseValue : 0m;
        damage = Hook.ModifyDamage(CombatState!, null, Owner.Creature,
            damage, ValueProp.Move, selected, null, out _);
        _damage += damage;
        await CardPileCmd.Exhaust(CombatState!, selected);
    }

    protected override void OnUpgrade() => _damage += 2m;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_damage);
}
