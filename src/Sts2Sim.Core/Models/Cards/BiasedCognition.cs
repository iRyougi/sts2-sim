using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BiasedCognition : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _focus = 5m;
    private decimal _decay = 1m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<FocusPower>(CombatState!, Owner.Creature, _focus, Owner.Creature, this);
        await PowerCmd.Apply<BiasedCognitionPower>(CombatState!, Owner.Creature, _decay, Owner.Creature, this);
    }
    protected override void OnUpgrade() => _focus += 1m;
}
