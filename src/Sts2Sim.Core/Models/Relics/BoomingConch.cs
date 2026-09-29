using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BoomingConch : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) =>
        ReferenceEquals(player, Owner) &&
        Owner.PlayerCombatState?.TurnNumber <= 1 &&
        Owner.RunState.CurrentRoom is CombatRoom { RoomType: RoomType.Elite }
            ? originalCardCount + 2m
            : originalCardCount;

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (Owner.PlayerCombatState?.TurnNumber <= 1 &&
            participants.Contains(Owner.Creature) &&
            Owner.RunState.CurrentRoom is CombatRoom { RoomType: RoomType.Elite })
        {
            Owner.PlayerCombatState.GainEnergy(1m);
        }

        return Task.CompletedTask;
    }
}