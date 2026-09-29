using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MachineLearning : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: 1);
    protected override Task OnPlay(CardPlay play) =>
        PowerCmd.Apply<MachineLearningPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
