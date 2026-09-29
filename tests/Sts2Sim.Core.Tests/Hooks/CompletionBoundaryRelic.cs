using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Hooks;

public sealed class CompletionBoundaryRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public Action? Reenter { get; set; }
    public bool ReentryRejected { get; private set; }
    public TaskCompletionSource? VictoryRelease { get; set; }
    public int EndCount { get; private set; }
    public int VictoryCount { get; private set; }
    public int OfferCount { get; private set; }
    public bool EndSawCombatPowers { get; private set; }
    public bool VictorySawCleanedContext { get; private set; }
    public override Task AfterCombatEnd()
    {
        EndCount++;
        if (EndCount == 1 && Reenter is not null)
        {
            try { Reenter(); }
            catch (InvalidOperationException) { ReentryRejected = true; }
        }
        EndSawCombatPowers = Owner.Creature.HasPower<StrengthPower>() && Owner.Creature.Block == 9;
        return Task.CompletedTask;
    }
    public override async Task AfterCombatVictory()
    {
        VictoryCount++;
        if (VictoryRelease is not null) await VictoryRelease.Task;
        VictorySawCleanedContext = EndCount == 1 && Owner.Creature.Powers.Count == 0 &&
            Owner.Creature.Block == 0 && Owner.PlayerCombatState is { } state &&
            state.AllPiles.All(pile => pile.Cards.Count == 0) && Owner.Creature.CombatState is not null;

    }
    public override Task BeforeCombatRewardOffered(RewardsSet rewards, CombatRoom room)
    {
        if (VictorySawCleanedContext) OfferCount++;
        return Task.CompletedTask;
    }
}
