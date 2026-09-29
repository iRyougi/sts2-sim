using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class RipAndTear : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = IsUpgraded ? 9m : 7m;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.RandomEnemy;
    protected override int CanonicalEnergyCost => 1;

    protected override Task OnPlay(CardPlay cardPlay) =>
        DamageCmd.Attack(IsUpgraded ? 9m : 7m).FromCard(this, cardPlay)
            .WithHitCount(2).TargetingRandomOpponents(CombatState!).Execute();
}
