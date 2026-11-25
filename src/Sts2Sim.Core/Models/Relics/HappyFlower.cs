using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HappyFlower : RelicModel
{
    private const int TurnsThreshold = 3; // DynamicVar "Turns"

    private int _turnCounter;

    // v0.114.0（#1110）：BeforeSideTurnStart 只记"本回合要不要给能量"，能量在 AfterPlayerTurnStartEarly 给。
    private bool _grantEnergyThisTurn;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        // Source: TurnsSeen = (TurnsSeen + 1) % Turns; Status = TurnsSeen == Turns - 1 ? Active : Normal;
        // GrantEnergyThisTurn = TurnsSeen == 0.
        _turnCounter = (_turnCounter + 1) % TurnsThreshold;
        Status = _turnCounter == TurnsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
        _grantEnergyThisTurn = _turnCounter == 0;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStartEarly(Player player)
    {
        if (!ReferenceEquals(player, Owner) || !_grantEnergyThisTurn)
        {
            return;
        }

        _grantEnergyThisTurn = false;
        await PlayerCmd.GainEnergy(1m, Owner);
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
        builder.Append(_grantEnergyThisTurn);
    }
}
