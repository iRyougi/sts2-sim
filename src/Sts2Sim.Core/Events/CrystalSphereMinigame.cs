using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Events;

/// <summary>偏离 #284：用坐标与大小工具动作驱动11x11网格，替代Godot窗口与动画。</summary>
public sealed class CrystalSphereMinigame
{
    private readonly Player _owner;
    private readonly Rng _rng;
    private readonly bool[,] _hidden = new bool[11, 11];
    private readonly CrystalSphereItem?[,] _occupants = new CrystalSphereItem?[11, 11];
    private readonly List<CrystalSphereItem> _items = [];
    private readonly List<CrystalSphereItem> _revealed = [];
    private readonly SemaphoreSlim _revealGate = new(1, 1);
    public int Width => 11;
    public int Height => 11;
    public int DivinationCount { get; private set; }
    public bool PlacedAllItems { get; private set; }
    public IReadOnlyList<CrystalSphereItem> Items => _items.AsReadOnly();
    public IReadOnlyList<CrystalSphereItem> RevealedItems => _revealed.AsReadOnly();
    public IReadOnlyList<Reward> Rewards { get; private set; } = [];

    public CrystalSphereMinigame(Player owner, Rng rng, int divinationCount)
    {
        _owner = owner;
        _rng = rng;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++) _hidden[x, y] = true;
        List<(int X, int Y)> corners = [(0, 0), (10, 0), (10, 10), (0, 10)];
        for (int i = 0; i < 2; i++)
            corners = corners.Concat(corners.SelectMany(p => Horizontal(p.X, p.Y)))
                .Concat(corners.SelectMany(p => Vertical(p.X, p.Y))).ToList();
        foreach (var (x, y) in corners) _hidden[x, y] = false;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            PlacedAllItems = PopulateItems();
            if (PlacedAllItems) break;
        }
        DivinationCount = divinationCount;
    }

    public bool IsHidden(int x, int y) { ValidateCell(x, y); return _hidden[x, y]; }

    public async Task RevealAsync(int x, int y, bool big = true)
    {
        ValidateCell(x, y);
        await _revealGate.WaitAsync();
        try
        {
            if (DivinationCount <= 0) throw new InvalidOperationException("The sphere has no remaining divinations.");
            DivinationCount--;
            IEnumerable<(int X, int Y)> cells = big
                ? Horizontal(x, y).Concat(Vertical(x, y)).Concat(Diagonal(x, y)).Append((x, y))
                : [(x, y)];
            foreach (var cell in cells) await Clear(cell.X, cell.Y);
            if (DivinationCount == 0)
            {
                // The source creates rewards only after all divinations; Doubt is applied immediately when revealed.
                // OneOffSynchronizer first calls ToReward for every item. Only potion construction
                // draws immediately; card and relic draws occur in the later Populate pass.
                var prepared = _revealed.Where(item => item.Kind != CrystalSphereItemKind.Curse)
                    .Select(item => (Item: item, Potion: item.Kind == CrystalSphereItemKind.Potion
                        ? CreateReward(item) : null)).ToArray();
                var rewards = prepared.Select(p => p.Potion ?? CreateReward(p.Item)).ToList();
                rewards.Sort((a, b) => RewardOrder(a).CompareTo(RewardOrder(b)));
                Rewards = rewards.AsReadOnly();
            }
        }
        finally { _revealGate.Release(); }
    }

    internal CrystalSphereMinigame CloneExactForRun(
        Player owner, Rng rng,
        Func<CrystalSphereItem, CrystalSphereItem> mapItem,
        Func<Reward, Reward> mapReward,
        Action<CrystalSphereMinigame, CrystalSphereMinigame> registerGame)
    {
        if (_revealGate.CurrentCount != 1)
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                "A sphere reveal is still executing.");
        return new CrystalSphereMinigame(this, owner, rng, mapItem, mapReward, registerGame);
    }

    private CrystalSphereMinigame(CrystalSphereMinigame source, Player owner, Rng rng,
        Func<CrystalSphereItem, CrystalSphereItem> mapItem,
        Func<Reward, Reward> mapReward,
        Action<CrystalSphereMinigame, CrystalSphereMinigame> registerGame)
    {
        _owner = owner;
        _rng = rng;
        // Publish before reward/card closures can point back to this same game.
        registerGame(source, this);
        Array.Copy(source._hidden, _hidden, source._hidden.Length);
        DivinationCount = source.DivinationCount;
        PlacedAllItems = source.PlacedAllItems;
        _items.AddRange(source._items.Select(mapItem));
        _revealed.AddRange(source._revealed.Select(mapItem));
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                _occupants[x, y] = source._occupants[x, y] is { } item ? mapItem(item) : null;
        Rewards = Array.AsReadOnly(source.Rewards.Select(mapReward).ToArray());
        // Fresh reveal gate; no RevealAsync/Clear/CreateReward/curse or RNG draws.
    }

    internal CrystalSphereMinigame CloneReseededForRun(
        Player owner, Rng rng, Func<CrystalSphereItem, CrystalSphereItem> mapVisibleItem,
        Func<Reward, Reward> mapReward,
        Action<CrystalSphereMinigame, CrystalSphereMinigame> registerGame,
        Action<CrystalSphereItem, CrystalSphereItem> registerVisibleItem)
    {
        if (_revealGate.CurrentCount != 1)
            throw new RunCloneNotSupportedException(RunCloneRejectionReason.PendingCallback,
                "A sphere reveal is still executing.");
        if (DivinationCount == 0)
        {
            // No board outcome can be consumed after the final divination. Retain only
            // public items and frozen rewards, not the old concealed item catalogue.
            return CloneResolvedVisibleForRun(owner, rng, mapVisibleItem, mapReward, registerGame);
        }

        var observed = new Dictionary<CrystalSphereItem, int>(ReferenceEqualityComparer.Instance);
        foreach (CrystalSphereItem item in _revealed)
            observed[item] = observed.GetValueOrDefault(item) + 1;
        while (true)
        {
            // A fresh native constructor determines its own placements, short-circuit
            // failures, ten-batch limit and accumulated subscriptions. Never read the
            // source's _items, _occupants, batch count or RNG state to drive generation.
            var candidate = new CrystalSphereMinigame(owner, rng, DivinationCount);
            if (candidate.PlacedAllItems != PlacedAllItems) continue;
            var visible = new Dictionary<CrystalSphereItem, CrystalSphereItem>(ReferenceEqualityComparer.Instance);
            bool matches = true;
            foreach (CrystalSphereItem item in candidate._items)
            {
                if (item.Position is not { } position) continue;
                bool fullyVisible = true;
                for (int dx = 0; dx < item.Width; dx++)
                    for (int dy = 0; dy < item.Height; dy++)
                        if (_hidden[position.X + dx, position.Y + dy]) fullyVisible = false;
                if (!fullyVisible) continue;
                CrystalSphereItem? prior = null;
                foreach (var observation in observed)
                {
                    CrystalSphereItem known = observation.Key;
                    if (known.Position != item.Position || known.Kind != item.Kind ||
                        known.CardRarity != item.CardRarity || known.PotionRarity != item.PotionRarity ||
                        known.IsBigGold != item.IsBigGold || known.Width != item.Width ||
                        known.Height != item.Height || observation.Value != item.RevealSubscriptions) continue;
                    prior = known;
                    break;
                }
                if (prior is null || !visible.TryAdd(prior, item)) { matches = false; break; }
            }
            if (!matches || visible.Count != observed.Count) continue;

            registerGame(this, candidate);
            Array.Copy(_hidden, candidate._hidden, _hidden.Length);
            foreach (var pair in visible)
            {
                pair.Value.IsRevealed = true;
                registerVisibleItem(pair.Key, pair.Value);
            }
            foreach (CrystalSphereItem item in _revealed) candidate._revealed.Add(visible[item]);
            candidate.Rewards = Array.AsReadOnly(Rewards.Select(mapReward).ToArray());
            // No Clear/RevealAsync/CreateReward is invoked: visible curse/card side effects
            // already occurred in the source and must not execute again in the branch.
            return candidate;
        }
    }

    private CrystalSphereMinigame(Player owner, Rng rng)
    {
        _owner = owner;
        _rng = rng;
    }

    private CrystalSphereMinigame CloneResolvedVisibleForRun(Player owner, Rng rng,
        Func<CrystalSphereItem, CrystalSphereItem> mapVisibleItem,
        Func<Reward, Reward> mapReward,
        Action<CrystalSphereMinigame, CrystalSphereMinigame> registerGame)
    {
        var target = new CrystalSphereMinigame(owner, rng);
        registerGame(this, target);
        Array.Copy(_hidden, target._hidden, _hidden.Length);
        target.DivinationCount = DivinationCount;
        target.PlacedAllItems = PlacedAllItems;
        var included = new HashSet<CrystalSphereItem>(ReferenceEqualityComparer.Instance);
        foreach (CrystalSphereItem known in _revealed)
        {
            CrystalSphereItem copied = mapVisibleItem(known);
            target._revealed.Add(copied);
            if (!included.Add(known)) continue;
            target._items.Add(copied);
            if (copied.Position is not { } position) continue;
            for (int dx = 0; dx < copied.Width; dx++)
                for (int dy = 0; dy < copied.Height; dy++)
                    target._occupants[position.X + dx, position.Y + dy] = copied;
        }
        target.Rewards = Array.AsReadOnly(Rewards.Select(mapReward).ToArray());
        return target;
    }


    private bool PopulateItems()
    {
        List<CrystalSphereItem> batch = [new(CrystalSphereItemKind.Relic),
            new(CrystalSphereItemKind.Potion), new(CrystalSphereItemKind.Potion),
            new(CrystalSphereItemKind.Potion, potionRarity: PotionRarity.Rare),
            new(CrystalSphereItemKind.CardReward, CardRarity.Common),
            new(CrystalSphereItemKind.CardReward, CardRarity.Uncommon),
            new(CrystalSphereItemKind.CardReward, CardRarity.Rare), new(CrystalSphereItemKind.Curse)];
        batch.AddRange(Enumerable.Range(0, 5).Select(_ => new CrystalSphereItem(CrystalSphereItemKind.Gold)));
        batch.AddRange(Enumerable.Range(0, 2).Select(_ => new CrystalSphereItem(CrystalSphereItemKind.Gold, bigGold: true)));
        bool placed = true;
        foreach (var item in batch)
        {
            // Source retains previous placements and short-circuits later placement attempts after a failure.
            placed = placed && Place(item);
            _items.Add(item);
        }
        foreach (var item in _items) item.RevealSubscriptions++;
        return placed;
    }

    private bool Place(CrystalSphereItem item)
    {
        var candidates = new List<(int X, int Y)>();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                if (CanPlace(item, x, y)) candidates.Add((x, y));
        if (candidates.Count == 0) return false;
        var position = _rng.NextItem(candidates);
        item.Position = position;
        for (int dx = 0; dx < item.Width; dx++)
            for (int dy = 0; dy < item.Height; dy++)
                _occupants[position.X + dx, position.Y + dy] = item;
        return true;
    }

    private bool CanPlace(CrystalSphereItem item, int x, int y)
    {
        if (x + item.Width > Width || y + item.Height > Height) return false;
        for (int dx = 0; dx < item.Width; dx++)
            for (int dy = 0; dy < item.Height; dy++)
                if (!_hidden[x + dx, y + dy] || _occupants[x + dx, y + dy] is not null) return false;
        return true;
    }

    private async Task Clear(int x, int y)
    {
        if (!_hidden[x, y]) return;
        _hidden[x, y] = false;
        var item = _occupants[x, y];
        if (item is null || item.IsRevealed || item.Position is not { } position) return;
        for (int dx = 0; dx < item.Width; dx++)
            for (int dy = 0; dy < item.Height; dy++)
                if (_hidden[position.X + dx, position.Y + dy]) return;
        item.IsRevealed = true;
        for (int i = 0; i < item.RevealSubscriptions; i++) _revealed.Add(item);
        if (item.Kind == CrystalSphereItemKind.Curse)
            await CardPileCmd.AddCursesToDeck([ModelDb.Card<Doubt>()], _owner);
    }

    private Reward CreateReward(CrystalSphereItem item)
    {
        switch (item.Kind)
        {
            case CrystalSphereItemKind.Gold:
                return new GoldReward(item.IsBigGold ? 30 : 10, _owner);
            case CrystalSphereItemKind.Potion:
                var potion = _rng.NextItem(PotionFactory.GetOutOfCombatPool(_owner)
                    .Where(p => p.Rarity == item.PotionRarity))!;
                return new PotionReward((PotionModel)potion.MutableClone(), _owner);
            case CrystalSphereItemKind.Relic:
                var rarity = RelicFactory.RollRarity(_rng);
                var relic = RelicFactory.PullNextRelicFromFront(_owner, rarity);
                return new RelicReward((RelicModel)relic.MutableClone(), _owner);
            case CrystalSphereItemKind.CardReward:
                var options = new CardCreationOptions([_owner.Character.CardPool], CardCreationSource.Other,
                    CardRarityOddsType.Uniform, c => c.Rarity == item.CardRarity).WithRngOverride(_rng);
                var reward = new CardReward(_owner, options);
                reward.Populate(_owner.RunState);
                return reward;
            default: throw new InvalidOperationException("A curse has no selectable reward.");
        }
    }
    private static int RewardOrder(Reward reward) => reward switch
    {
        GoldReward => 1, PotionReward => 2, RelicReward => 3, CardReward => 5, _ => 6,
    };
    private static IEnumerable<(int X, int Y)> Horizontal(int x, int y)
    {
        if (x > 0) yield return (x - 1, y);
        if (x < 10) yield return (x + 1, y);
    }
    private static IEnumerable<(int X, int Y)> Vertical(int x, int y)
    {
        if (y > 0) yield return (x, y - 1);
        if (y < 10) yield return (x, y + 1);
    }
    private static IEnumerable<(int X, int Y)> Diagonal(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx += 2)
            for (int dy = -1; dy <= 1; dy += 2)
                if (x + dx is >= 0 and < 11 && y + dy is >= 0 and < 11) yield return (x + dx, y + dy);
    }
    private static void ValidateCell(int x, int y)
    {
        if (x is < 0 or >= 11 || y is < 0 or >= 11) throw new ArgumentOutOfRangeException(nameof(x));
    }
}
