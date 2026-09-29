using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat.StateDescription;

internal enum CombatPrivateStateClassification
{
    Contributor,
    DerivedPublicState,
    TransientBoundary,
    IgnoredNoFutureBehavior,
}
