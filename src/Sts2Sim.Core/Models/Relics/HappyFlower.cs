using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HappyFlower : RelicModel
{
    private const int TurnsThreshold = 3; // DynamicVar "Turns"

    private int _turnCounter;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        _turnCounter++;
        // Source: TurnsSeen = (TurnsSeen + 1) % Turns; Status = TurnsSeen == Turns - 1 ? Active : Normal.
        Status = _turnCounter == TurnsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
        if (_turnCounter < 3)
        {
            return Task.CompletedTask;
        }

        _turnCounter = 0;
        Owner.PlayerCombatState!.GainEnergy(1m);
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_turnCounter);
    }
}
