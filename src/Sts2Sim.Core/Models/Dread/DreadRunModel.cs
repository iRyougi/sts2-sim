using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Dread;

public sealed class DreadRunModel : RunModel
{
    // v0.114.0: the dominion moved from AfterActEntered to the new BeforeActEntered (applied before the act's first
    // room); AfterActEntered only adds Ascender's Bane from the second act on.
    public override Task BeforeActEntered()
    {
        ApplyCurrentDominion();
        return Task.CompletedTask;
    }

    public override async Task AfterActEntered()
    {
        if (RunState is not RunState runState || runState.CurrentActIndex <= 0)
        {
            return;
        }

        CardModel ascendersBane = ModelDb.Card<AscendersBane>();
        foreach (Player player in runState.Players)
        {
            await CardPileCmd.AddCursesToDeck([ascendersBane], player);
        }
    }

    public override Task AfterRunLoaded()
    {
        ApplyCurrentDominion();
        return Task.CompletedTask;
    }

    public override Task BeforeRoomEntered(AbstractRoom room)
    {
        if (!RemoveDominionIfRoomIsBoss(room) &&
            RunState is RunState runState &&
            runState.VisitedMapCoords.Count == 0 &&
            runState.Dominion is null)
        {
            ApplyCurrentDominion();
            if (room is CombatRoom combatRoom)
            {
                combatRoom.CombatState?.SetDominion(runState.Dominion);
            }
        }

        return Task.CompletedTask;
    }

    private bool RemoveDominionIfRoomIsBoss(AbstractRoom room)
    {
        if (room is not CombatRoom combatRoom ||
            combatRoom.RoomType != RoomType.Boss ||
            combatRoom.Encounter is not { } encounter ||
            RunState is not RunState runState)
        {
            return false;
        }

        if (encounter.IdEntry == runState.CurrentActBossEncounter?.IdEntry ||
            (runState.CurrentActSecondBossEncounter is { } second && encounter.IdEntry == second.IdEntry))
        {
            runState.SetDominion(null);
            combatRoom.CombatState?.SetDominion(null);
            return true;
        }

        return false;
    }

    internal void ApplyCurrentDominion()
    {
        if (RunState is not RunState runState)
        {
            return;
        }

        EncounterDefinition? bossEncounter = runState.CurrentActSecondBossEncounter ?? runState.CurrentActBossEncounter;
        DominionModel? canonical = bossEncounter?.GetDominion();
        runState.SetDominion(canonical);
    }
}
