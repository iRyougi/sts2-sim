using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class StoneArmor : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _plating = 4m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<PlatingPower>(CombatState!, Owner.Creature, _plating,
            Owner.Creature, this);
    }

    protected override void OnUpgrade() => _plating += 2m;
}
