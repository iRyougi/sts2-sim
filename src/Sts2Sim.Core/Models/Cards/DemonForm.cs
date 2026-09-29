using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class DemonForm : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    private decimal _strengthPerTurn = 3m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<DemonFormPower>(CombatState!, Owner.Creature, _strengthPerTurn, Owner.Creature, this);
    protected override void OnUpgrade() => _strengthPerTurn++;
}
