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
