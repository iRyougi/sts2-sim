using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>每回合首次集齐 3 种旋律时获得 ConductorsBatonPower（原版 <c>ConductorsBaton</c>）。</summary>
public sealed class ConductorsBaton : RelicModel
{
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _triggeredThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner &&
            Owner.Creature.CombatState is { } combatState &&
            combatState.IsLiveCombat() &&
            !_triggeredThisTurn &&
            MelodyCmd.MelodiesThisTurn(Owner) >= 3)
        {
            _triggeredThisTurn = true;
            Flash();
            await PowerCmd.Apply<ConductorsBatonPower>(
                combatState, Owner.Creature, 1m, Owner.Creature, cardSource: null);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(_triggeredThisTurn);
}
