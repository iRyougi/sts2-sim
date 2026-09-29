using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class PrepTime : CardModel
{
    private decimal _vigor = 4m;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override void OnUpgrade() => _vigor += 2m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<PrepTimePower>(
            CombatState!, Owner.Creature, _vigor, Owner.Creature, this);
    }
}
