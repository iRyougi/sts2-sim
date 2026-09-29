using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CreativeAi : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _power = 1m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<CreativeAiPower>(CombatState!, Owner.Creature, _power, Owner.Creature, this);
    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
