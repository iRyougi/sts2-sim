using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeHappyFlower : RelicModel
{
    private const int TurnsThreshold = 5; // DynamicVar "Turns"

    private int _turnsSeen;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override int MerchantCost => 50;

    public int TurnsSeen => _turnsSeen;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature))
        {
            return Task.CompletedTask;
        }

        _turnsSeen = (_turnsSeen + 1) % 5;
        Status = _turnsSeen == TurnsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
        if (_turnsSeen == 0)
        {
            Owner.PlayerCombatState!.GainEnergy(1m);
        }

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
        builder.Append(_turnsSeen);
    }
}
