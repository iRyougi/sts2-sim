using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Beckon : CardModel
{
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => 1;
    protected override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand()
    {
        await CreatureCmd.Damage(
            CombatState ?? throw new InvalidOperationException("Beckon turn-end damage requires active combat."),
            new[] { Owner.Creature }, 6m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature, this, null);
    }
}
