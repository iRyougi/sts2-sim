using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Inferno : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    private decimal _damage = 6m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        InfernoPower? power = await PowerCmd.Apply<InfernoPower>(
            CombatState!, Owner.Creature, _damage, Owner.Creature, this);
        power?.IncrementSelfDamage();
    }

    protected override void OnUpgrade() => _damage += 3m;
}
