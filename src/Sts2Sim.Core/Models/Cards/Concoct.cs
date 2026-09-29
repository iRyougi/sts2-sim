using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Concoct : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyAlly;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        return PowerCmd.Apply<ConcoctPower>(CombatState!, cardPlay.Target, IsUpgraded ? 4m : 3m, Owner.Creature, this);
    }
}
