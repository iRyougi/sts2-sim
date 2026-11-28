using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Dread.Dominions;

public sealed class WaterfallGiantDominion : DominionModel
{
    private const int WeakExplosiveAmount = 4;
    private const int StrongExplosiveAmount = 5;
    private const int EliteExplosiveAmount = 7;
    private const int MultiExplosiveAmount = 3;

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom { CombatState: { } combatState })
        {
            int amount = GetExplosiveAmount(combatState);
            foreach (Creature enemy in combatState.Enemies.ToArray())
            {
                await PowerCmd.Apply<ExplosivePower>(
                    combatState, enemy, amount, applier: null, cardSource: null);
            }
        }
    }

    protected override async Task AfterEnemiesEnteredCombat(IEnumerable<Creature> creatures)
    {
        Creature[] eligible = creatures.Where(c => !c.HasPower<MinionPower>()).ToArray();
        // Native WaterfallGiantDominion.AfterEnemiesEnteredCombat: no-op unless the run's current room is a CombatRoom;
        // the amount comes from that room's encounter (copied onto the CombatState when the room prepared it).
        if (eligible.Length == 0 || eligible[0].CombatState is not CombatState combatState
            || combatState.RunState.CurrentRoom is not CombatRoom)
        {
            return;
        }

        int amount = GetExplosiveAmount(combatState);
        foreach (Creature enemy in eligible)
        {
            await PowerCmd.Apply<ExplosivePower>(
                combatState, enemy, amount, applier: null, cardSource: null);
        }
    }

    private static int GetExplosiveAmount(CombatState combatState)
    {
        int initialCount = combatState.InitialEncounterMonsterCount > 0
            ? combatState.InitialEncounterMonsterCount
            : combatState.Enemies.Count;
        if (initialCount > 1)
        {
            return MultiExplosiveAmount;
        }

        if (combatState.Encounter?.IsWeak == true)
        {
            return WeakExplosiveAmount;
        }

        if (combatState.EncounterRoomType == RoomType.Elite)
        {
            return EliteExplosiveAmount;
        }

        return StrongExplosiveAmount;
    }
}
