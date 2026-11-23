using Sts2Sim.Core.Combat;

namespace Sts2Sim.Core.Runs;

/// <summary>Optionally validates the final live combat state before outcome and rewards are resolved.</summary>
public interface ICombatCompletionValidator
{
    void ValidateCompletedCombat(CombatState state);
}
