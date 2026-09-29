using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Once during the owner's side turn, heal damage actually received.</summary>
public sealed class DemonTongue : RelicModel
{
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        _ = props;
        _ = dealer;
        _ = cardSource;
        Creature owner = Owner.Creature;
        if (owner.CombatState is not { } combatState ||
            combatState.CurrentSide != owner.Side ||
            !ReferenceEquals(target, owner) ||
            result.UnblockedDamage <= 0 || _triggeredThisTurn)
            return;

        _triggeredThisTurn = true;
        await CreatureCmd.Heal(owner, result.UnblockedDamage);
    }

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        _ = side;
        if (participants.Contains(Owner.Creature))
            _triggeredThisTurn = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_triggeredThisTurn);
    }
}
