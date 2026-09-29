using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Content;

/// <summary>
/// Decision-time exclusions for cross-character effects that still need end-to-end parity evidence.
/// All five playable character card pools are registered. <c>Kaleidoscope</c> uses the other four:
/// the #296 replay found identical Niche ledgers in 57/57 archived runs, but different card reward
/// candidates in 53/57 (#303). <c>Splash</c> also uses other-character pools and was not retested in #296;
/// its separate free-play deviation #97 remains. Both stay excluded from default decisions until
/// their player-facing effects are verified.
///
/// <c>PrismaticGem</c> now uses the native reward-pool union (#226/#292);
/// <c>ColorfulPhilosophers</c> exists and has its own #202 scope. This registry contains only the
/// two types below; their status does not imply the other two are fully verified.
///
/// Exclusions are consulted at decision time. Candidate generation and RNG draws remain intact,
/// so this policy cannot silently change the simulator's random stream.
/// </summary>
public static class CrossCharacterContentExclusions
{
    private static readonly HashSet<Type> ExcludedTypes = new()
    {
        typeof(Splash),
        typeof(Kaleidoscope),
    };

    public static bool IsExcluded(Type type) => ExcludedTypes.Contains(type);

    public static bool IsExcluded(AbstractModel model) => IsExcluded(model.GetType());

    /// <summary>Event options don't carry a reference to the relic/card they grant — only a string
    /// key. For <see cref="Models.AncientEventModel"/>-style relic options (Neow), that key is the
    /// relic's own <c>Type.Name</c> (see <c>AncientEventModel.RelicOption</c>), so matching on name is
    /// exact for the content this registry actually covers today.</summary>
    public static bool IsExcludedKey(string key) => ExcludedTypes.Any(type => type.Name == key);
}
