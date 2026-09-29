using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Clash : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = IsUpgraded ? 18m : 14m;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsPlayable => Owner.PlayerCombatState!.Hand.Cards.All(card => card.Type == CardType.Attack);

    protected override Task OnPlay(CardPlay cardPlay) =>
        DamageCmd.Attack(IsUpgraded ? 18m : 14m).FromCard(this, cardPlay).Targeting(cardPlay.Target!).Execute();
}
