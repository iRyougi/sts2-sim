using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class RollingBoulder : CardModel
{
    private decimal _startingDamage = 5m;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 3;

    protected override void OnUpgrade() => _startingDamage += 5m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<RollingBoulderPower>(
            CombatState!, Owner.Creature, _startingDamage, Owner.Creature, this);
    }
}
