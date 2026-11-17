using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>第一回合开始时获得 6 层 StretchPower（原版 <c>ExerciseMat</c>）。</summary>
public sealed class ExerciseMat : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState?.TurnNumber <= 1 &&
            Owner.Creature.CombatState is { } combatState)
        {
            Flash();
            await PowerCmd.Apply<StretchPower>(
                combatState, Owner.Creature, 6m, Owner.Creature, cardSource: null);
        }
    }
}
