using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BodySlam : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Owner.Creature.Block;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        return DamageCmd.Attack(Owner.Creature.Block).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
