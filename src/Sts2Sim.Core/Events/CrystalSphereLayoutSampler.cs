using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Events;

/// <summary>
/// #979 (deviation): regenerates the concealed part of a Crystal Sphere board for a reseeded
/// imagination clone. Items with at least one uncovered cell are known (the player sees that
/// part of the picture); every other item must lie wholly under fog. The native process places
/// a fixed batch in order, each item uniformly among the cells that were hidden and free when
/// the board was built. This sampler proposes the same process restricted to placements that
/// agree with the known items, weights each particle by native/proposal probability and keeps
/// one by sampling-importance-resampling over a fixed particle count, so the time is bounded.
/// Sequential-mode board generation (<see cref="CrystalSphereMinigame"/>) does not use this.
/// </summary>
internal static class CrystalSphereLayoutSampler
{
    /// <summary>Particles are drawn in rounds of this size until the effective sample size is reached.</summary>
    internal const int RoundSize = 64;
    internal const int TargetEffectiveSampleSize = 128;
    internal const int MaxParticles = 4096;
    internal const int MaxBatches = 10;
    private const int Side = 11;

    internal readonly record struct Shape(CrystalSphereItemKind Kind, CardRarity CardRarity, PotionRarity PotionRarity, bool BigGold)
    {
        internal static Shape Of(CrystalSphereItem item) => new(item.Kind, item.CardRarity, item.PotionRarity, item.IsBigGold);
        internal CrystalSphereItem Create() => new(Kind, CardRarity, PotionRarity, BigGold);
    }

    internal readonly record struct Known(Shape Shape, int X, int Y);

    /// <summary>Positions of every created item in creation order (batch-major), null when unplaced.</summary>
    internal sealed record Layout((int X, int Y)?[] Positions, int Batches, bool PlacedAll, int[] KnownSlots);

    internal readonly record struct Diagnostics(int Particles, int DeadParticles, int Rounds, double EffectiveSampleSize);

    /// <summary>The native batch, in the order the source constructs and places it.</summary>
    internal static readonly Shape[] Batch =
    [
        new(CrystalSphereItemKind.Relic, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Potion, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Potion, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Potion, CardRarity.None, PotionRarity.Rare, false),
        new(CrystalSphereItemKind.CardReward, CardRarity.Common, PotionRarity.Common, false),
        new(CrystalSphereItemKind.CardReward, CardRarity.Uncommon, PotionRarity.Common, false),
        new(CrystalSphereItemKind.CardReward, CardRarity.Rare, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Curse, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Gold, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Gold, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Gold, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Gold, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Gold, CardRarity.None, PotionRarity.Common, false),
        new(CrystalSphereItemKind.Gold, CardRarity.None, PotionRarity.Common, true),
        new(CrystalSphereItemKind.Gold, CardRarity.None, PotionRarity.Common, true),
    ];

    private static readonly (int W, int H)[] Dims = Batch.Select(shape =>
    {
        CrystalSphereItem probe = shape.Create();
        return (probe.Width, probe.Height);
    }).ToArray();

    /// <summary>Cells still hidden when the native constructor places items (all but the cleared corners).</summary>
    internal static bool[,] BuildTimeHidden()
    {
        var hidden = new bool[Side, Side];
        for (int x = 0; x < Side; x++)
            for (int y = 0; y < Side; y++) hidden[x, y] = true;
        List<(int X, int Y)> corners = [(0, 0), (10, 0), (10, 10), (0, 10)];
        for (int i = 0; i < 2; i++)
            corners = corners.Concat(corners.SelectMany(p => Cross(p.X, p.Y))).Distinct().ToList();
        foreach (var (x, y) in corners) hidden[x, y] = false;
        return hidden;
    }

    private static IEnumerable<(int X, int Y)> Cross(int x, int y)
    {
        if (x > 0) yield return (x - 1, y);
        if (x < Side - 1) yield return (x + 1, y);
        if (y > 0) yield return (x, y - 1);
        if (y < Side - 1) yield return (x, y + 1);
    }

    /// <param name="failedAt">Index (within the first batch) of the first item the board could not place,
    /// or null when every item was placed. Unplaced items are drawn on the cleared top-left corner, so this is visible.</param>
    internal static Layout Sample(bool[,] hidden, IReadOnlyList<Known> known, int? failedAt, Rng rng,
        out Diagnostics diagnostics, int targetEss = TargetEffectiveSampleSize, int maxParticles = MaxParticles)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxParticles, 1);
        var context = new Context(hidden, known, failedAt);
        // With nothing known, nothing uncovered and no visible failure the proposal is the native
        // process itself (every weight is 1); one draw then matches the native constructor bit for bit.
        int roundSize = context.Unconstrained ? 1 : Math.Min(RoundSize, maxParticles);
        var particles = new List<(Layout Layout, double LogWeight)>();
        int drawn = 0, rounds = 0;
        double ess = 0;
        double[] weights = [];
        while (drawn < maxParticles)
        {
            rounds++;
            for (int i = 0; i < roundSize && drawn < maxParticles; i++, drawn++)
                if (context.TryParticle(rng, out Layout? layout, out double logWeight)) particles.Add((layout!, logWeight));
            if (particles.Count == 0) continue;
            double max = particles.Max(p => p.LogWeight);
            weights = particles.Select(p => Math.Exp(p.LogWeight - max)).ToArray();
            double sum = weights.Sum();
            ess = sum * sum / weights.Sum(w => w * w);
            if (context.Unconstrained || ess >= targetEss) break;
        }
        diagnostics = new Diagnostics(drawn, drawn - particles.Count, rounds, ess);
        if (particles.Count == 0)
            throw new InvalidOperationException(
                $"Crystal Sphere conditional layout found no consistent particle among {drawn} " +
                $"({known.Count} known items, failedAt={failedAt?.ToString() ?? "none"}).");
        if (particles.Count == 1) return particles[0].Layout;
        double target = rng.NextDouble() * weights.Sum();
        for (int i = 0; i < weights.Length; i++)
        {
            target -= weights[i];
            if (target < 0) return particles[i].Layout;
        }
        return particles[^1].Layout;
    }

    internal sealed class Context
    {
        private readonly bool[,] _buildHidden = BuildTimeHidden();
        private readonly bool[,] _allowed = new bool[Side, Side];
        private readonly IReadOnlyList<Known> _known;
        private readonly List<int>[] _knownByShape;
        private readonly List<int>[] _slotsByShape;
        private readonly int[,] _occupied = new int[Side, Side];
        private readonly List<(int X, int Y)> _proposal = new(Side * Side);
        private readonly int? _failedAt;

        public bool Unconstrained { get; }

        public Context(bool[,] hidden, IReadOnlyList<Known> known, int? failedAt)
        {
            _known = known;
            _failedAt = failedAt;
            for (int x = 0; x < Side; x++)
                for (int y = 0; y < Side; y++) _allowed[x, y] = hidden[x, y];
            Shape[] distinct = Batch.Distinct().ToArray();
            _knownByShape = new List<int>[Batch.Length];
            _slotsByShape = new List<int>[Batch.Length];
            for (int slot = 0; slot < Batch.Length; slot++)
            {
                int group = Array.IndexOf(Batch, Batch[slot]);
                (_slotsByShape[group] ??= []).Add(slot);
                _knownByShape[group] ??= [];
            }
            for (int k = 0; k < known.Count; k++)
            {
                int group = Array.IndexOf(Batch, known[k].Shape);
                if (group < 0) throw new InvalidOperationException($"Unknown Crystal Sphere item shape {known[k].Shape}.");
                _knownByShape[group].Add(k);
                var (w, h) = Dims[group];
                for (int dx = 0; dx < w; dx++)
                    for (int dy = 0; dy < h; dy++)
                    {
                        int x = known[k].X + dx, y = known[k].Y + dy;
                        if (x >= Side || y >= Side || !_buildHidden[x, y])
                            throw new InvalidOperationException($"Known Crystal Sphere item {known[k]} lies outside the build-time board.");
                        _allowed[x, y] = false;
                    }
            }
            foreach (int group in distinct.Select(shape => Array.IndexOf(Batch, shape)))
                if (_knownByShape[group].Count > _slotsByShape[group].Count)
                    throw new InvalidOperationException($"More known {Batch[group]} items than native slots.");
            bool same = known.Count == 0 && failedAt is null;
            for (int x = 0; x < Side && same; x++)
                for (int y = 0; y < Side && same; y++) same = _allowed[x, y] == _buildHidden[x, y];
            Unconstrained = same;
        }

        public bool TryParticle(Rng rng, out Layout? layout, out double logWeight)
        {
            layout = null;
            logWeight = 0;
            // Uniform injective assignment of known items to native slots of the same shape.
            var knownAt = new int[Batch.Length];
            Array.Fill(knownAt, -1);
            int[] knownSlots = new int[_known.Count];
            for (int group = 0; group < Batch.Length; group++)
            {
                List<int>? knownItems = _knownByShape[group];
                if (knownItems is null || knownItems.Count == 0) continue;
                int[] slots = _slotsByShape[group].ToArray();
                for (int i = 0; i < knownItems.Count; i++)
                {
                    int j = slots.Length - i > 1 ? rng.NextInt(i, slots.Length) : i;
                    (slots[i], slots[j]) = (slots[j], slots[i]);
                    knownAt[slots[i]] = knownItems[i];
                    knownSlots[knownItems[i]] = slots[i];
                }
            }
            for (int x = 0; x < Side; x++)
                for (int y = 0; y < Side; y++) _occupied[x, y] = -1;
            var positions = new List<(int X, int Y)?>(Batch.Length);
            bool placedAll = false;
            int batches = 0;
            while (batches < MaxBatches && !placedAll)
            {
                batches++;
                bool placing = true;
                for (int slot = 0; slot < Batch.Length; slot++)
                {
                    int index = positions.Count;
                    int knownIndex = batches == 1 ? knownAt[slot] : -1;
                    if (!placing)
                    {
                        // Native short-circuit: later items are created but never placed.
                        if (knownIndex >= 0) return false;
                        positions.Add(null);
                        continue;
                    }
                    var (w, h) = Dims[slot];
                    int native = 0;
                    _proposal.Clear();
                    for (int x = 0; x <= Side - w; x++)
                        for (int y = 0; y <= Side - h; y++)
                        {
                            if (!CanBuild(x, y, w, h)) continue;
                            native++;
                            if (knownIndex < 0 && Allowed(x, y, w, h)) _proposal.Add((x, y));
                        }
                    if (knownIndex >= 0)
                    {
                        Known item = _known[knownIndex];
                        if (!CanBuild(item.X, item.Y, w, h)) return false;
                        logWeight -= Math.Log(native);
                        Occupy(item.X, item.Y, w, h, index);
                        positions.Add((item.X, item.Y));
                        continue;
                    }
                    if (native == 0)
                    {
                        placing = false;
                        positions.Add(null);
                        continue;
                    }
                    if (_proposal.Count == 0) return false;
                    var chosen = _proposal[rng.NextInt(0, _proposal.Count)];
                    logWeight += Math.Log(_proposal.Count) - Math.Log(native);
                    Occupy(chosen.X, chosen.Y, w, h, index);
                    positions.Add(chosen);
                }
                placedAll = placing;
                // The visible outcome of the first batch must match: placed in full, or failing at the same item.
                if (batches == 1 && (placing ? _failedAt is not null : positions.IndexOf(null) != _failedAt)) return false;
            }
            layout = new Layout(positions.ToArray(), batches, placedAll, knownSlots);
            return true;
        }

        private bool CanBuild(int x, int y, int w, int h)
        {
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                    if (!_buildHidden[x + dx, y + dy] || _occupied[x + dx, y + dy] >= 0) return false;
            return true;
        }

        private bool Allowed(int x, int y, int w, int h)
        {
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++)
                    if (!_allowed[x + dx, y + dy]) return false;
            return true;
        }

        private void Occupy(int x, int y, int w, int h, int index)
        {
            for (int dx = 0; dx < w; dx++)
                for (int dy = 0; dy < h; dy++) _occupied[x + dx, y + dy] = index;
        }
    }
}
