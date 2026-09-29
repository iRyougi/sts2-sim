using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Content;

/// <summary>
/// Decision-time exclusions for content whose cross-character behavior still has unresolved fidelity gaps.
///
/// The authoritative game has exactly four such mechanisms (confirmed via a full-text search of
/// <c>CharacterCardPools</c> across the entire decompiled source, 2026-08-29): <c>PrismaticGem</c>
/// (relic), <c>ColorfulPhilosophers</c> (event), <c>Splash</c> (card), <c>Kaleidoscope</c> (relic).
/// <c>PrismaticGem</c> now uses the native reward-pool union. <c>Splash</c> and
/// <c>Kaleidoscope</c> retain decision-time exclusions pending their separate fidelity audits.
///
/// This registry is deliberately kept separate from the core game-rule layer (card pool generation,
/// <c>IsAllowedAtNeow</c>, RNG consumption) — excluding content there would shrink candidate pools
/// *before* the RNG draw that picks from them, which shifts the RNG-to-outcome mapping for every draw
/// from that pool, not just the excluded ones, breaking Plan07's sim-real bit-exact parity far more
/// broadly than intended. Instead, this list is consulted purely at decision time (see
/// <see cref="Runs.IRunDecisionSource"/>'s default reward/event heuristics) — the underlying simulator
/// still generates and offers this content exactly like the real game (preserving RNG fidelity); the
/// decision layer just declines to pick it.
///
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
