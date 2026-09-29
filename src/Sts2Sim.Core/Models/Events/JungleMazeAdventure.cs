using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 event with a costly solo quest or a safer group effort. Deviation #84:
/// SOLO_QUEST omits shuffling the three visual _fx entries: exactly two additional draws from
/// this event's local RNG in upstream v0.111.0. JOIN_FORCES does not run this shuffle.
/// Plan 08b-4 source audit bounds the omitted draws to that event-local stream; it does not
/// establish runtime parity for SOLO_QUEST or imply a shift in subsequent rooms' run RNG.</summary>
public sealed class JungleMazeAdventure : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.Count == 1 ||
        runState.Players.All(player => player.Creature.CurrentHp > 18m);

    private const decimal SoloQuestDamage = 18m;

    private decimal _soloGold;
    private decimal _joinForcesGold;

    protected override void CalculateVars()
    {
        _soloGold = 150m + (decimal)Rng.NextFloat(-15f, 15f);
        _joinForcesGold = 50m + (decimal)Rng.NextFloat(-15f, 15f);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("SOLO_QUEST", SoloQuestAsync),
        new EventOption("JOIN_FORCES", JoinForcesAsync),
    };

    private async Task SoloQuestAsync()
    {
        await CreatureCmd.LoseHp(
            RunState,
            Owner.Creature,
            SoloQuestDamage,
            ValueProp.Unblockable);
        await PlayerCmd.GainGold(_soloGold, Owner);
        Finish();
    }

    private async Task JoinForcesAsync()
    {
        await PlayerCmd.GainGold(_joinForcesGold, Owner);
        Finish();
    }
}
