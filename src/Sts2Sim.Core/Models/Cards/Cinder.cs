using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Cinder : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    private decimal _damage = 18m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        CardModel? selected = Owner.RunState.Rng.CombatCardSelection.NextItem(
            Owner.PlayerCombatState!.Hand.Cards);
        if (selected is not null) await CardPileCmd.Exhaust(CombatState!, selected);
    }

    protected override void OnUpgrade() => _damage += 6m;
}
