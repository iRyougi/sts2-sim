using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Rage : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _power = 3m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<RagePower>(CombatState!, Owner.Creature, _power, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _power += 2m;
}
