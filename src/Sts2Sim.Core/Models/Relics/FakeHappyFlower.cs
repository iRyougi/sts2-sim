using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeHappyFlower : RelicModel
{
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
        if (_turnsSeen == 0)
        {
            Owner.PlayerCombatState!.GainEnergy(1m);
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_turnsSeen);
    }
}
