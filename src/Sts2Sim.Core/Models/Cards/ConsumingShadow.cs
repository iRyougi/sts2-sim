using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ConsumingShadow : CardModel, ICardChoiceBaseValueProvider
{
    private int _repeat = 2;
    private decimal _power = 1m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < _repeat; i++)
            await OrbCmd.Channel<DarkOrb>(CombatState!, Owner);
        await PowerCmd.Apply<ConsumingShadowPower>(CombatState!, Owner.Creature, _power, Owner.Creature, this);
    }
    protected override void OnUpgrade() => _repeat += 1;
}
