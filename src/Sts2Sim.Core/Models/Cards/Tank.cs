using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Tank : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues();
    public override bool IsMultiplayerOnly => true;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<TankPower>(CombatState!, Owner.Creature, 1m,
            Owner.Creature, this);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
