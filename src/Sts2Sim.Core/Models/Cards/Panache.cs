using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Panache : CardModel
{
    private decimal _damage = 10m;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override void OnUpgrade() => _damage += 4m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<PanachePower>(
            CombatState!, Owner.Creature, _damage, Owner.Creature, this);
    }
}
