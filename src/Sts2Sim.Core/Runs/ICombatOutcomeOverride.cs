using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs;

/// <summary>Imagination-only result; surviving HP is applied after victory resolution.</summary>
public sealed record InjectedCombatOutcome(bool Died, int RemainingHp);

/// <summary>Optional decision-source capability. Null runs the combat normally. Deviation #331.</summary>
public interface ICombatOutcomeOverride
{
    InjectedCombatOutcome? TryInject(RunState run, CombatRoom room);
}
