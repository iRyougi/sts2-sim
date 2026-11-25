using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeHappyFlower : RelicModel
{
    private const int TurnsThreshold = 5; // DynamicVar "Turns"

    private int _turnsSeen;

    // v0.114.0（#1110）：BeforeSideTurnStart 只记"本回合要不要给能量"，能量在 AfterPlayerTurnStartEarly 给。
    private bool _grantEnergyThisTurn;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override int MerchantCost => 50;

    public int TurnsSeen => _turnsSeen;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        _turnsSeen = (_turnsSeen + 1) % TurnsThreshold;
        Status = _turnsSeen == TurnsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
        _grantEnergyThisTurn = _turnsSeen == 0;
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
        builder.Append(_turnsSeen);
        builder.Append(_grantEnergyThisTurn);
    }
}
