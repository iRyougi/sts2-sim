using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MiniRegent : RelicModel
{
    private bool _triggeredThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterStarsSpent(int amount, Player spender)
    {
        if (amount <= 0 ||
            spender != Owner ||
            _triggeredThisTurn)
        {
            return;
        }

        _triggeredThisTurn = true;
        await PowerCmd.Apply<StrengthPower>(
            Owner.Creature.CombatState!,
            Owner.Creature,
            1m,
            Owner.Creature,
            null);
    }

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _triggeredThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
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
