using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Rebound : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = IsUpgraded ? 12m : 9m;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await DamageCmd.Attack(IsUpgraded ? 12m : 9m).FromCard(this, cardPlay).Targeting(cardPlay.Target!).Execute();
        await PowerCmd.Apply<ReboundPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
}
