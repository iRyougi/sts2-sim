using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Genesis : CardModel
{
    private decimal _starsPerTurn = 2m;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    protected override void OnUpgrade() => _starsPerTurn += 1m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<GenesisPower>(
            CombatState!, Owner.Creature, _starsPerTurn, Owner.Creature, this);
    }
}
