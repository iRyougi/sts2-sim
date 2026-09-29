using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Content;

/// <summary>
/// Content whose real effect draws from other characters' card pools (<c>UnlockState.CharacterCardPools</c>
/// in the authoritative source), which this project cannot faithfully reproduce yet because Phase 1 only
/// ports the Regent's own content — other characters' card pools don't exist in <see cref="ModelDb"/> at all.
///
/// The authoritative game has exactly four such mechanisms (confirmed via a full-text search of
/// <c>CharacterCardPools</c> across the entire decompiled source, 2026-08-29): <c>PrismaticGem</c>
/// (relic), <c>ColorfulPhilosophers</c> (event), <c>Splash</c> (card), <c>Kaleidoscope</c> (relic).
/// <c>PrismaticGem</c>/<c>ColorfulPhilosophers</c> are not implemented in this project at all, so they're
/// structurally unreachable already — nothing to register here. <c>Splash</c> and <c>Kaleidoscope</c>
/// *are* implemented, but their "other character" step degrades into "draw from the Regent's own pool
/// again" (see their own deviation comments, #98 and #187) — which is a materially different, and
/// currently unfixable, effect from the real game. Letting the AI obtain either of them during training
/// would teach it a behavior around a broken effect that won't match the real game.
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
/// If <c>PrismaticGem</c>/<c>ColorfulPhilosophers</c> are ever implemented, add them here in the same
/// pass — don't let this list quietly fall out of sync with what's actually reachable.
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
