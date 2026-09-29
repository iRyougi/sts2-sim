using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Unmovable : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues();

    protected override async Task OnPlay(CardPlay cardPlay) =>
        await PowerCmd.Apply<UnmovablePower>(CombatState!, Owner.Creature, 1m,
            Owner.Creature, this);

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
