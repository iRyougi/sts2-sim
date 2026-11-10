using System.Collections.Concurrent;
using System.Collections.Immutable;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Observed public effect facts of one card (#752). A non-null reason marks effects that could not be fully observed.</summary>
public sealed record CardEffectFacts(ImmutableDictionary<string, float> Values, string? UnsupportedReason)
{
    public bool Supported => UnsupportedReason is null;
}

/// <summary>
/// #752: describes a card by what its Core model does in a fixed sandbox combat (Silent vs a 999-HP
/// TrainingDummy, 10 energy, 10 stars, starter deck, fixed sandbox seed, choices take the first offered
/// cards). Playable cards are played once; the deltas are damage dealt, block gained, HP lost, cards drawn
/// (by type), created (by type), exhausted, discarded, potions and gold gained, energy/star change and every power
/// applied to self or enemy with its amount -- the facts printed on the card face (draw, next-turn block,
/// generated Shivs, applied power and stacks), observed instead of hand-tabulated. Unplayable cards are
/// held through one turn end (and chosen, if choosable) and compared with the same sandbox without them;
/// they stay marked unsupported because conditional/out-of-combat effects remain unobserved.
/// The sandbox seed is a content-description constant: it never reads, clones or reseeds a run's RNG.
/// Conditional/scaling effects are observed in this one standard context only. Unsupported comes only from
/// deterministic causes (not playable, choosable, a deterministic exception of the card model); a timeout is
/// load-dependent, so it throws and is never cached -- training and deployment can never disagree about it.
/// </summary>
public static class CardEffectProbe
{
    public const string SandboxSeed = "CARD-EFFECT-PROBE";
    private const int ProbeHp = 999, ProbeResource = 10;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<(string Id, int Upgrade), CardEffectFacts> Cache = new();
    private static readonly Lazy<Dictionary<string, float>> HeldBaseline = new(() => ItemEffectProbe.StartDedicated(() => HeldAsync(null)).GetAwaiter().GetResult());

    public static readonly ImmutableArray<string> CountNames =
    [
        "DamageDealt", "BlockGained", "HpLost", "CardsDrawn", "DrawnAttack", "DrawnSkill", "DrawnOther",
        "CardsCreated", "CreatedAttack", "CreatedSkill", "CreatedPower", "CreatedOther", "OthersExhausted",
        "OthersDiscarded", "SelfExhausted", "PotionsGained", "GoldChange", "EnergyChange", "StarsChange", "Selections",
    ];

    /// <summary>Every registered power, ordinal by ID; a version update changes this list and therefore the schema.</summary>
    public static ImmutableArray<string> PowerIds() =>
        [.. ModelDb.All<PowerModel>().Select(power => power.Id.Entry).Distinct().Order(StringComparer.Ordinal)];

    public static CardEffectFacts Probe(string cardId, int upgradeLevel) =>
        Cache.GetOrAdd((cardId, upgradeLevel), key => Observe(key.Id, key.Upgrade));

    private static CardEffectFacts Observe(string cardId, int upgradeLevel)
    {
        Task<CardEffectFacts> probe = ItemEffectProbe.StartDedicated(() => ProbeAsync(cardId, upgradeLevel));
        try
        {
            return probe.Wait(Timeout) ? probe.Result
                : throw new TimeoutException($"Card effect probe for {cardId}+{upgradeLevel} did not finish; not cached.");
        }
        catch (AggregateException exception)
        {
            // fail-open: an unobservable card becomes an explicit Unsupported fact that encoders flag and count.
            return new(ImmutableDictionary<string, float>.Empty, "exception:" + exception.InnerException?.GetType().Name);
        }
    }

    private static async Task<CardEffectFacts> ProbeAsync(string cardId, int upgradeLevel)
    {
        var sandbox = await Sandbox.CreateAsync(cardId, upgradeLevel);
        CardModel card = sandbox.Card!;
        // Choosable statuses (Knowledge Demon) carry their effect in OnChosen, not OnPlay.
        if (!card.CanPlay(out _) || card is KnowledgeDemon.IChoosable)
        {
            var held = await HeldAsync((cardId, upgradeLevel));
            var baseline = HeldBaseline.Value;
            var relative = held.Keys.Union(baseline.Keys)
                .Select(key => (key, value: held.GetValueOrDefault(key) - baseline.GetValueOrDefault(key)))
                .Where(item => item.value != 0).ToImmutableDictionary(item => item.key, item => item.value);
            return new(relative, card is KnowledgeDemon.IChoosable ? "choosable" : "not-playable");
        }
        var before = Snapshot(sandbox);
        var selections = new SelectionCounter();
        using (CardSelectCmd.PushTraceObserver(selections))
            await sandbox.Engine.PlayCardAsync(sandbox.Player, card, card.TargetType is TargetType.AnyEnemy ? sandbox.Dummy : null);
        var values = Diff(before, Snapshot(sandbox), card, selections.Count);
        return values.Values.All(float.IsFinite)
            ? new(values.ToImmutableDictionary(), null)
            : new(ImmutableDictionary<string, float>.Empty, "non-finite");
    }

    /// <summary>Holds the card (or nothing, for the baseline) in hand through one turn end; choosable cards are chosen first.</summary>
    private static async Task<Dictionary<string, float>> HeldAsync((string Id, int Upgrade)? card)
    {
        var sandbox = await Sandbox.CreateAsync(card?.Id, card?.Upgrade ?? 0);
        var before = Snapshot(sandbox);
        var selections = new SelectionCounter();
        using (CardSelectCmd.PushTraceObserver(selections))
        {
            if (sandbox.Card is KnowledgeDemon.IChoosable choosable) await choosable.OnChosen();
            await sandbox.Engine.EndPlayerTurnAsync();
        }
        return Diff(before, Snapshot(sandbox), sandbox.Card, selections.Count);
    }

    private static Dictionary<string, float> Diff(State before, State after, CardModel? card, int selections)
    {
        var values = new Dictionary<string, float>();
        void Add(string name, float value)
        {
            if (value != 0) values[name] = values.GetValueOrDefault(name) + value;
        }
        bool Other(CardModel c) => !ReferenceEquals(c, card);
        HashSet<CardModel> created = new(after.Cards.Where(c => !before.Cards.Contains(c) && Other(c)), ReferenceEqualityComparer.Instance);
        Add("DamageDealt", before.EnemyHp - after.EnemyHp + Math.Max(0, before.EnemyBlock - after.EnemyBlock));
        Add("BlockGained", Math.Max(0, after.PlayerBlock - before.PlayerBlock));
        Add("HpLost", Math.Max(0, before.PlayerHp - after.PlayerHp));
        foreach (CardModel c in after.Hand.Where(c => before.Draw.Contains(c)))
        {
            Add("CardsDrawn", 1);
            Add(c.Type switch { CardType.Attack => "DrawnAttack", CardType.Skill => "DrawnSkill", _ => "DrawnOther" }, 1);
        }
        foreach (CardModel c in created)
        {
            Add("CardsCreated", 1);
            Add(c.Type switch { CardType.Attack => "CreatedAttack", CardType.Skill => "CreatedSkill",
                CardType.Power => "CreatedPower", _ => "CreatedOther" }, 1);
        }
        Add("OthersExhausted", after.Exhaust.Count(c => !before.Exhaust.Contains(c) && Other(c) && !created.Contains(c)));
        Add("OthersDiscarded", after.Discard.Count(c => before.Hand.Contains(c) && Other(c)));
        Add("SelfExhausted", card is not null && after.Exhaust.Contains(card) ? 1 : 0);
        Add("PotionsGained", after.Potions - before.Potions);
        Add("GoldChange", after.Gold - before.Gold);
        Add("EnergyChange", after.Energy - before.Energy);
        Add("StarsChange", after.Stars - before.Stars);
        Add("Selections", selections);
        foreach (var (side, b, a) in new[] { ("Self", before.PlayerPowers, after.PlayerPowers), ("Enemy", before.EnemyPowers, after.EnemyPowers) })
            foreach (string id in a.Keys.Union(b.Keys))
                Add($"Power.{side}.{id}", (float)(a.GetValueOrDefault(id) - b.GetValueOrDefault(id)));
        return values;
    }

    private sealed record Sandbox(CombatEngine Engine, Player Player, Creature Dummy, CardModel? Card)
    {
        public static async Task<Sandbox> CreateAsync(string? cardId, int upgradeLevel)
        {
            var run = new SandboxRunState();
            // Choice effects resolve with a fixed "first offered cards" pick; the choice itself is counted as Selections.
            var combat = new CombatState(run) { CardSelectionSource = new FirstCardsSelector() };
            Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
            run.AddPlayer(player);
            Creature dummy = combat.AddMonster((MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
            var engine = new CombatEngine(combat);
            await engine.StartCombatAsync();
            dummy.SetMaxHpInternal(ProbeHp);
            dummy.SetCurrentHpInternal(ProbeHp);
            var state = player.PlayerCombatState!;
            state.Energy = ProbeResource;
            state.GainStars(ProbeResource - state.Stars);
            if (cardId is null) return new(engine, player, dummy, null);
            var card = (CardModel)ModelDb.GetById<CardModel>(new ModelId("CARD", cardId)).MutableClone();
            for (int i = 0; i < upgradeLevel; i++) card.Upgrade();
            card.AssignOwner(player);
            await CardPileCmd.Generate(combat, card, PileType.Hand);
            return new(engine, player, dummy, card);
        }
    }

    private sealed record State(int EnemyHp, int EnemyBlock, int PlayerHp, int PlayerBlock, int Energy, int Stars, int Potions, int Gold,
        HashSet<CardModel> Hand, HashSet<CardModel> Draw, HashSet<CardModel> Discard, HashSet<CardModel> Exhaust,
        HashSet<CardModel> Cards, Dictionary<string, decimal> PlayerPowers, Dictionary<string, decimal> EnemyPowers);

    private static State Snapshot(Sandbox sandbox)
    {
        Player player = sandbox.Player;
        var s = player.PlayerCombatState!;
        HashSet<CardModel> Set(CardPile pile) => new(pile.Cards, ReferenceEqualityComparer.Instance);
        static Dictionary<string, decimal> Powers(Creature creature) => creature.Powers
            .GroupBy(power => power.Id.Entry).ToDictionary(group => group.Key, group => (decimal)group.Sum(power => power.Amount));
        return new(sandbox.Dummy.CurrentHp, sandbox.Dummy.Block, player.Creature.CurrentHp, player.Creature.Block, s.Energy, s.Stars,
            player.PotionSlots.Count(potion => potion is not null), player.Gold, Set(s.Hand), Set(s.DrawPile), Set(s.DiscardPile), Set(s.ExhaustPile),
            new(s.AllPiles.SelectMany(pile => pile.Cards), ReferenceEqualityComparer.Instance), Powers(player.Creature), Powers(sandbox.Dummy));
    }

    private sealed class FirstCardsSelector : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>([.. request.Candidates.Take(Math.Max(request.MinCount, Math.Min(1, request.MaxCount)))]);
    }

    private sealed class SelectionCounter : ICardSelectionTraceObserver
    {
        public int Count { get; private set; }
        public void BeforeSelection(CardSelectionRequest request) { }
        public void AfterSelection(CardSelectionRequest request, IReadOnlyList<CardModel> selected, bool automatic) => Count++;
    }

    private sealed class SandboxRunState : IRunState
    {
        private readonly List<Player> _players = [];
        public AscensionManager Ascension { get; } = new(0);
        public RunRngSet Rng { get; } = new(SandboxSeed);
        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;
        public Rooms.AbstractRoom? CurrentRoom => null;
        public void AddPlayer(Player player) => _players.Add(player);
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? [];
    }
}
