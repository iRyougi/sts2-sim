using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Pendulum : RelicModel
{
    private const int TurnsThreshold = 3; // DynamicVar "Turns"

    private int _turnCounter;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        // 偏离 #114：无独立 AfterPlayerTurnStart；用摸牌后的 AfterSideTurnStart 过滤到持有者的玩家回合。
        if (!participants.Contains(Owner.Creature))
        {
            return;
        }

        _turnCounter++;
        // Source (BeforeHandDraw): TurnsSeen = (TurnsSeen + 1) % Turns; Status = TurnsSeen == Turns - 1 ? Active : Normal.
        // This counter resets to 0 right below instead of wrapping, so apply the modulo here.
        Status = _turnCounter % TurnsThreshold == TurnsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
        if (_turnCounter < 3)
        {
            return;
        }

        _turnCounter = 0;
        await CardPileCmd.Draw(
            Owner.Creature.CombatState!,
            1,
            Owner,
            fromHandDraw: false);
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
