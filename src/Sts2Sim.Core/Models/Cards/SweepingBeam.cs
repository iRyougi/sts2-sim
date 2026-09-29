using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SweepingBeam : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 6m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage, Cards: 1);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
        await CardPileCmd.Draw(CombatState!, 1, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() => _damage += 3m;
}
