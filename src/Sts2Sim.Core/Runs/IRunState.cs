using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Runs;

/// <summary>run 态接口。偏离 #37：新增 Rng/Players,均是 Task 14 CombatEngine 驱动战斗所需的最小面。</summary>
public interface IRunState
{
    IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState);

    ICardSelectionDecisionSource CardSelectionSource =>
        RunEngineCardSelectionDecisionSource.Instance;

    RunRngSet Rng { get; }

    AscensionManager Ascension { get; }

    IReadOnlyList<Player> Players { get; }

    /// <summary>跨幕累计的已访问房间数。</summary>
    int TotalFloor { get; }

    AbstractRoom? CurrentRoom { get; }

    /// <summary>The first room entered at the current map point. Roomless facades default to null;
    /// implementations with rooms must expose their actual stack rather than approximate CurrentRoom.</summary>
    AbstractRoom? BaseRoom => null;
}
