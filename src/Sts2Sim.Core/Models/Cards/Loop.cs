using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Loop : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _amount = 1m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    protected override Task OnPlay(CardPlay play) =>
        PowerCmd.Apply<LoopPower>(CombatState!, Owner.Creature, _amount, Owner.Creature, this);
    protected override void OnUpgrade() => _amount += 1m;
}
