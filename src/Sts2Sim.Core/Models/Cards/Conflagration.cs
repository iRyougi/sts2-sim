using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Conflagration : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = 2m;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: 2);
    private int _repeats = 4;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay cardPlay) => DamageCmd.Attack(2m)
        .WithHitCount(_repeats).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
    protected override void OnUpgrade() => _repeats++;
}
