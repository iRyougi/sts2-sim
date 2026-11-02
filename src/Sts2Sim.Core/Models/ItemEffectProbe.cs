using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models;

/// <summary>Observed public effect facts of one relic or potion (#762). Non-empty reasons mark effects that could not be fully observed.</summary>
public sealed record ItemEffectFacts(ImmutableDictionary<string, float> Values, ImmutableArray<string> UnsupportedReasons)
{
    public bool Supported => UnsupportedReasons.IsEmpty;
}

/// <summary>
/// #762: describes relics and potions by what their Core models do in a fixed sandbox, the same method as
/// <see cref="CardEffectProbe"/>. Sandbox: Silent (999 max HP, at half) with the starter deck against two
/// 999-HP TrainingDummies, fixed content seed, choices take the first offered cards.
/// A relic is obtained and carried through a fixed script whose phases are its trigger conditions:
/// Obtain (out of combat), CombatStart (combat-room entry through the first turn's draw), Attack (ten generated
/// Strikes on the first dummy), TurnEnd (three rounds of player turn end, both dummies attack, next turn start),
/// Skill (ten generated Defends), Poison (one Deadly Poison on the first dummy) and Victory (both dummies at 1 HP
/// struck down, then combat end and victory hooks). Each phase's effect is its state change minus the same phase
/// without the relic; effect types are damage, block, HP, max HP, gold, energy, stars, draw/creation by type, potions, deck
/// changes and applied powers (self, first enemy by power, other enemy as total stacks), so "first enemy" versus
/// "other enemy" separates single from all-enemy effects. Counts keep their phase (turn ends accumulate turn by turn,
/// plus the energy at each turn start); a power keeps its largest-magnitude phase change, so a decaying debuff is not
/// cancelled by its decay, and Victory-phase power clearing is ignored.
/// A potion is used once in combat (enemy-targeted on the first dummy, ally-targeted on the player) and
/// described by the same deltas. Overridden hooks the script never reaches (map, shop, rest site, rewards, ...;
/// room entry is reached for the combat room only) and automatic potions are listed as unsupported reasons: an unobserved effect is never reported as no effect.
/// The sandbox seed is a content-description constant: it never reads, clones or reseeds a run's RNG.
/// A timeout is load-dependent, so it throws and is never cached.
/// </summary>
public static class ItemEffectProbe
{
    public const string SandboxSeed = "ITEM-EFFECT-PROBE";
    private const int ProbeHp = 999, ProbeEnergy = 10;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly ConcurrentDictionary<string, ItemEffectFacts> RelicCache = new(), PotionCache = new();
    private static readonly Lazy<Dictionary<string, float>[]> RelicBaseline =
        new(() => Task.Run(() => RunRelicScriptAsync(null)).GetAwaiter().GetResult());
    private static readonly Lazy<Dictionary<string, float>> PotionBaseline =
        new(() => Task.Run(() => UsePotionAsync(null)).GetAwaiter().GetResult());

    public static readonly ImmutableArray<string> Phases = ["Obtain", "CombatStart", "Attack", "TurnEnd", "Skill", "Poison", "Victory"];
    // Every-N triggers up to ten plays / three turns can show in the script.
    private const int ScriptedPlays = 10, ScriptedTurns = 3;

    public static readonly ImmutableArray<string> CountNames =
    [
        "DamageDealt", "DamageDealtOthers", "BlockGained", "HpLost", "HpGained", "MaxHpChange", "CardsDrawn",
        "DrawnAttack", "DrawnSkill", "DrawnOther", "CardsCreated", "CreatedAttack", "CreatedSkill", "CreatedPower",
        "CreatedOther", "Exhausted", "Discarded", "PotionsGained", "GoldChange", "EnergyChange", "StarsChange",
        "Selections", "DeckCardsAdded", "DeckUpgrades", "CombatUpgrades", "PotionSlotsChange", "RelicsGained",
        "EnemyOtherPowerStacks", "TurnStartEnergy",
    ];

    /// <summary>Hooks the relic script reaches. Any other override of a model hook is an unsupported reason.</summary>
    private static readonly ImmutableHashSet<string> ScriptedHooks =
    [
        "AfterObtained", "BeforeCombatStart", "BeforeCombatStartLate", "AfterCreatureAddedToCombat",
        "BeforeSideTurnStart", "AfterSideTurnStart", "AfterSideTurnStartLate", "AfterPlayerTurnStartEarly",
        "AfterPlayerTurnStart", "AfterPlayerTurnStartLate", "AfterEnergyReset", "AfterEnergyResetLate",
        "ShouldPlayerResetEnergy", "ModifyMaxEnergy", "BeforeHandDraw", "ModifyHandDraw", "AfterModifyingHandDraw",
        "ShouldDraw", "AfterCardDrawnEarly", "AfterCardDrawn", "AfterShuffle", "AfterAutoPrePlayPhaseEnteredEarly",
        "AfterAutoPrePlayPhaseEntered", "AfterAutoPrePlayPhaseEnteredLate", "AfterAutoPostPlayPhaseEntered",
        "AfterCardGenerated", "AfterCardGeneratedForCombat", "AfterCardEnteredCombat", "AfterCardChangedPiles",
        "AfterCardChangedPilesLate", "BeforeCardPlayed", "AfterCardPlayed", "AfterCardPlayedLate", "AfterEnergySpent",
        "ModifyCardPlayCount", "AfterModifyingCardPlayCount", "ModifyCardPlayResultLocation",
        "AfterModifyingCardPlayResultLocation", "BeforeAttack", "AfterAttack", "ModifyAttackHitCount",
        "ModifyDamageAdditive", "ModifyDamageMultiplicative", "ModifyDamageCap", "BeforeDamageReceived",
        "AfterDamageReceived", "AfterDamageGiven", "AfterCurrentHpChanged", "ModifyHpLostBeforeOsty",
        "ModifyHpLostBeforeOstyLate", "ModifyHpLostAfterOsty", "ModifyHpLostAfterOstyLate",
        "AfterModifyingHpLostBeforeOsty", "AfterModifyingHpLostAfterOsty", "ModifyUnblockedDamageTarget",
        "BeforeBlockGained", "AfterBlockGained", "AfterModifyingBlockAmount", "ModifyBlockAdditive",
        "ModifyBlockMultiplicative", "ShouldClearBlock", "AfterBlockCleared", "AfterBlockBroken",
        "BeforeSideTurnEndVeryEarly", "BeforeSideTurnEndEarly", "BeforeSideTurnEnd", "AfterSideTurnEnd",
        "AfterCardDiscarded", "AfterHandEmptied", "ModifyPowerAmountGivenAdditive", "ModifyPowerAmountGivenMultiplicative",
        "TryModifyPowerAmountReceived", "AfterModifyingPowerAmountReceived", "BeforePowerAmountChanged",
        "AfterPowerAmountChanged", "ShouldDie", "ShouldDieLate", "BeforeDeath", "AfterDeath", "ShouldStopCombatFromEnding",
        "ShouldCreatureBeRemovedFromCombatAfterDeath", "AfterCombatVictoryEarly", "AfterCombatVictory", "AfterCombatEnd",
        "ModifyGoldGained", "AfterGoldGained",
    ];

    /// <summary>Virtuals that are model plumbing or pool metadata, not gameplay hooks.</summary>
    private static readonly ImmutableHashSet<string> NotHooks =
    [
        "AfterCloned", "AfterRemoved", "DeepCloneFields", "AppendCombatStateDescription",
        "RestoreCombatCloneReferencesFrom", "IsAllowed", "IsAllowedAtNeow",
    ];

    /// <summary>Registered relic/potion IDs, ordinal; a version update changes these lists and therefore the schema.</summary>
    public static ImmutableArray<string> RelicIds() => Ids<RelicModel>();
    public static ImmutableArray<string> PotionIds() => Ids<PotionModel>();

    private static ImmutableArray<string> Ids<T>() where T : AbstractModel =>
        [.. ModelDb.All<T>().Select(model => model.Id.Entry).Distinct().Order(StringComparer.Ordinal)];

    public static ItemEffectFacts ProbeRelic(string relicId) =>
        RelicCache.GetOrAdd(relicId, id => Observe(() => RelicAsync(id), "relic " + id));

    public static ItemEffectFacts ProbePotion(string potionId) =>
        PotionCache.GetOrAdd(potionId, id => Observe(() => PotionAsync(id), "potion " + id));

    private static ItemEffectFacts Observe(Func<Task<ItemEffectFacts>> run, string label)
    {
        Task<ItemEffectFacts> probe = Task.Run(run);
        try
        {
            return probe.Wait(Timeout) ? probe.Result
                : throw new TimeoutException($"Item effect probe for {label} did not finish; not cached.");
        }
        catch (AggregateException exception)
        {
            // fail-open: an unobservable item becomes an explicit Unsupported fact that encoders flag and count.
            return new(ImmutableDictionary<string, float>.Empty, ["exception:" + exception.InnerException?.GetType().Name]);
        }
    }

    private static async Task<ItemEffectFacts> RelicAsync(string relicId)
    {
        var relic = ModelDb.GetById<RelicModel>(new ModelId("RELIC", relicId));
        var phases = await RunRelicScriptAsync(relic);
        var baseline = RelicBaseline.Value;
        var values = new Dictionary<string, float>();
        for (int p = 0; p < Phases.Length; p++)
            foreach (string key in phases[p].Keys.Union(baseline[p].Keys))
            {
                float delta = phases[p].GetValueOrDefault(key) - baseline[p].GetValueOrDefault(key);
                bool power = key.StartsWith("Power.", StringComparison.Ordinal) || key == "EnemyOtherPowerStacks";
                // Deaths and combat end clear powers in Victory; counting that would cancel powers applied earlier.
                if (delta == 0 || power && Phases[p] == "Victory") continue;
                if (key.StartsWith("Power.", StringComparison.Ordinal))
                {
                    // A power keeps its largest-magnitude phase change (earliest on ties): decay of a temporary
                    // debuff must not cancel its application, and negative applications (Strength down) survive.
                    if (Math.Abs(delta) > Math.Abs(values.GetValueOrDefault(key))) values[key] = delta;
                    continue;
                }
                string name = Phases[p] + "." + key;
                values[name] = values.GetValueOrDefault(name) + delta;
            }
        // The probed relic itself is not a gained relic.
        if (values.TryGetValue("Obtain.RelicsGained", out float gained))
        {
            if (gained == 1) values.Remove("Obtain.RelicsGained");
            else values["Obtain.RelicsGained"] = gained - 1;
        }
        var reasons = UnscriptedHooks(relic.GetType()).Select(hook => "hook:" + hook).ToList();
        return Facts(values, reasons);
    }

    /// <summary>An item with no observed delta is reported as unobserved, never as having no effect.</summary>
    private static ItemEffectFacts Facts(Dictionary<string, float> values, List<string> reasons)
    {
        if (!values.Values.All(float.IsFinite)) return new(ImmutableDictionary<string, float>.Empty, ["non-finite"]);
        var observed = values.Where(item => item.Value != 0).ToImmutableDictionary();
        if (observed.IsEmpty && reasons.Count == 0) reasons.Add("no-effect-observed");
        // A choice whose consequence the sandbox never sees (the picked card's later effect) is not a full fact (#762 D1).
        if (!observed.IsEmpty && observed.Keys.All(key => key == "Selections" || key.EndsWith(".Selections", StringComparison.Ordinal)))
            reasons.Add("selection-effect-not-observed");
        return new(observed, [.. reasons]);
    }

    /// <summary>Hook overrides declared below RelicModel that the script never reaches.</summary>
    private static IEnumerable<string> UnscriptedHooks(Type type)
    {
        var hooks = new SortedSet<string>(StringComparer.Ordinal);
        for (Type? t = type; t is not null && t != typeof(RelicModel); t = t.BaseType)
            foreach (MethodInfo method in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Type declaring = method.GetBaseDefinition().DeclaringType!;
                if ((declaring == typeof(AbstractModel) || declaring == typeof(RelicModel)) && method != method.GetBaseDefinition() &&
                    !method.IsSpecialName && !ScriptedHooks.Contains(method.Name) && !NotHooks.Contains(method.Name))
                    hooks.Add(method.Name == nameof(AbstractModel.AfterRoomEntered) ? "AfterRoomEntered:non-combat" : method.Name);
            }
        return hooks;
    }

    /// <summary>Per-phase deltas of the relic script; null runs the baseline without the relic.</summary>
    private static async Task<Dictionary<string, float>[]> RunRelicScriptAsync(RelicModel? relic)
    {
        var sandbox = Sandbox.Create();
        var results = new Dictionary<string, float>[Phases.Length];
        var selections = new SelectionCounter();
        using IDisposable observer = CardSelectCmd.PushTraceObserver(selections);
        async Task Phase(int index, Func<Task> action)
        {
            int before = selections.Count;
            var start = Snapshot(sandbox);
            await action();
            var diff = Diff(start, Snapshot(sandbox), selections.Count - before);
            results[index] = results[index] is { } sum
                ? sum.Keys.Union(diff.Keys).ToDictionary(key => key, key => sum.GetValueOrDefault(key) + diff.GetValueOrDefault(key))
                : diff;
        }
        await Phase(0, async () => { if (relic is not null) await RelicCmd.Obtain(relic, sandbox.Player); });
        State? setup = null;
        int beforeStart = selections.Count;
        await sandbox.Engine.StartCombatAsync(async () =>
        {
            foreach (Creature dummy in sandbox.Dummies) { dummy.SetMaxHpInternal(ProbeHp); dummy.SetCurrentHpInternal(ProbeHp); }
            setup = Snapshot(sandbox);
            await Hook.AfterRoomEntered(sandbox.Combat.RunState, sandbox.Room);
        });
        results[1] = Diff(setup!, Snapshot(sandbox), selections.Count - beforeStart);
        await Phase(2, () => sandbox.PlayEachAsync(ModelDb.Card<StrikeSilent>(), sandbox.Dummies[0], ScriptedPlays));
        // Turns are accumulated one by one so per-turn draws and turn-start energy do not telescope away.
        float turnStartEnergy = 0;
        for (int turn = 0; turn < ScriptedTurns && sandbox.Engine.IsInProgress; turn++)
        {
            await Phase(3, () => sandbox.Engine.EndPlayerTurnAsync());
            turnStartEnergy += sandbox.Player.PlayerCombatState!.Energy;
        }
        results[3]!["TurnStartEnergy"] = turnStartEnergy;
        await Phase(4, () => sandbox.PlayEachAsync(ModelDb.Card<DefendSilent>(), null, ScriptedPlays));
        await Phase(5, () => sandbox.PlayEachAsync(ModelDb.Card<DeadlyPoison>(), sandbox.Dummies[0], 1));
        foreach (Creature dummy in sandbox.Dummies) dummy.SetCurrentHpInternal(1);
        await Phase(6, async () =>
        {
            foreach (Creature dummy in sandbox.Dummies)
            {
                sandbox.Player.PlayerCombatState!.Energy = ProbeEnergy;
                if (sandbox.Engine.IsInProgress) await sandbox.PlayAsync(ModelDb.Card<StrikeSilent>(), dummy);
            }
            if (!sandbox.Engine.CheckWinCondition() || !sandbox.Engine.Won)
                throw new InvalidOperationException("Sandbox victory did not end the combat.");
            await Hook.AfterCombatEnd(sandbox.Combat);
            await Hook.AfterCombatVictory(sandbox.Combat);
        });
        return results;
    }

    private static async Task<ItemEffectFacts> PotionAsync(string potionId)
    {
        var potion = ModelDb.GetById<PotionModel>(new ModelId("POTION", potionId));
        if (potion.Usage == PotionUsage.Automatic)
            return new(ImmutableDictionary<string, float>.Empty, ["automatic"]);
        var used = await UsePotionAsync(potion);
        var baseline = PotionBaseline.Value;
        var values = used.Keys.Union(baseline.Keys)
            .ToDictionary(key => key, key => used.GetValueOrDefault(key) - baseline.GetValueOrDefault(key));
        return Facts(values, []);
    }

    /// <summary>Deltas of using one potion in a started sandbox combat (or doing nothing, for the baseline).
    /// The used potion leaving its slot is not counted as a lost potion.</summary>
    private static async Task<Dictionary<string, float>> UsePotionAsync(PotionModel? canonical)
    {
        var sandbox = Sandbox.Create();
        await sandbox.Engine.StartCombatAsync(() =>
        {
            foreach (Creature dummy in sandbox.Dummies) { dummy.SetMaxHpInternal(ProbeHp); dummy.SetCurrentHpInternal(ProbeHp); }
            return Task.CompletedTask;
        });
        sandbox.Player.PlayerCombatState!.Energy = ProbeEnergy;
        PotionModel? potion = null;
        if (canonical is not null)
        {
            potion = (PotionModel)canonical.MutableClone();
            potion.AssignOwner(sandbox.Player);
            sandbox.Player.AddPotionInternal(potion);
        }
        var before = Snapshot(sandbox);
        var selections = new SelectionCounter();
        using (CardSelectCmd.PushTraceObserver(selections))
            if (potion is not null)
                await PotionCmd.Use(potion, sandbox.Player, potion.TargetType switch
                {
                    TargetType.AnyEnemy => sandbox.Dummies[0],
                    TargetType.Self or TargetType.AnyPlayer or TargetType.AnyAlly => sandbox.Player.Creature,
                    _ => null,
                });
        var values = Diff(before, Snapshot(sandbox), selections.Count);
        if (potion is not null && !sandbox.Player.PotionSlots.Contains(potion))
            values["PotionsGained"] = values.GetValueOrDefault("PotionsGained") + 1;
        return values;
    }

    private static Dictionary<string, float> Diff(State before, State after, int selections)
    {
        var values = new Dictionary<string, float>();
        void Add(string name, float value)
        {
            if (value != 0) values[name] = values.GetValueOrDefault(name) + value;
        }
        HashSet<CardModel> created = new(after.Cards.Where(c => !before.Cards.Contains(c) && !after.Generated.Contains(c)), ReferenceEqualityComparer.Instance);
        Add("DamageDealt", Damage(before.Enemies[0], after.Enemies[0]));
        Add("DamageDealtOthers", before.Enemies.Skip(1).Zip(after.Enemies.Skip(1)).Sum(pair => Damage(pair.First, pair.Second)));
        Add("BlockGained", Math.Max(0, after.PlayerBlock - before.PlayerBlock));
        Add("HpLost", Math.Max(0, before.PlayerHp - after.PlayerHp));
        Add("HpGained", Math.Max(0, after.PlayerHp - before.PlayerHp));
        Add("MaxHpChange", after.PlayerMaxHp - before.PlayerMaxHp);
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
        Add("Exhausted", after.Exhaust.Count(c => !before.Exhaust.Contains(c) && !created.Contains(c)));
        Add("Discarded", after.Discard.Count(c => before.Hand.Contains(c)));
        Add("PotionsGained", after.Potions - before.Potions);
        Add("GoldChange", after.Gold - before.Gold);
        Add("EnergyChange", after.Energy - before.Energy);
        Add("StarsChange", after.Stars - before.Stars);
        Add("Selections", selections);
        Add("DeckCardsAdded", after.DeckCount - before.DeckCount);
        Add("DeckUpgrades", after.DeckUpgrades - before.DeckUpgrades);
        Add("CombatUpgrades", after.Cards.Count(c => c.IsUpgraded && before.Cards.Contains(c) && !before.Upgraded.Contains(c)));
        Add("PotionSlotsChange", after.PotionSlots - before.PotionSlots);
        Add("RelicsGained", after.Relics - before.Relics);
        foreach (string id in after.PlayerPowers.Keys.Union(before.PlayerPowers.Keys))
            Add("Power.Self." + id, (float)(after.PlayerPowers.GetValueOrDefault(id) - before.PlayerPowers.GetValueOrDefault(id)));
        foreach (string id in after.Enemies[0].Powers.Keys.Union(before.Enemies[0].Powers.Keys))
            Add("Power.Enemy." + id, (float)(after.Enemies[0].Powers.GetValueOrDefault(id) - before.Enemies[0].Powers.GetValueOrDefault(id)));
        Add("EnemyOtherPowerStacks", (float)before.Enemies.Skip(1).Zip(after.Enemies.Skip(1))
            .Sum(pair => pair.Second.Powers.Values.Sum() - pair.First.Powers.Values.Sum()));
        return values;
    }

    private static float Damage(Enemy before, Enemy after) => before.Hp - after.Hp + Math.Max(0, before.Block - after.Block);

    private sealed record Enemy(int Hp, int Block, Dictionary<string, decimal> Powers);

    private sealed record State(Enemy[] Enemies, int PlayerHp, int PlayerMaxHp, int PlayerBlock, int Energy, int Stars,
        int Potions, int PotionSlots, int Gold, int DeckCount, int DeckUpgrades, int Relics, HashSet<CardModel> Hand,
        HashSet<CardModel> Draw, HashSet<CardModel> Discard, HashSet<CardModel> Exhaust, HashSet<CardModel> Cards,
        HashSet<CardModel> Upgraded, HashSet<CardModel> Generated, Dictionary<string, decimal> PlayerPowers);

    private static State Snapshot(Sandbox sandbox)
    {
        Player player = sandbox.Player;
        var s = player.PlayerCombatState;
        HashSet<CardModel> Set(CardPile? pile) => new(pile?.Cards ?? [], ReferenceEqualityComparer.Instance);
        static Dictionary<string, decimal> Powers(Creature creature) => creature.Powers
            .GroupBy(power => power.Id.Entry).ToDictionary(group => group.Key, group => (decimal)group.Sum(power => power.Amount));
        HashSet<CardModel> cards = new(s?.AllPiles.SelectMany(pile => pile.Cards) ?? [], ReferenceEqualityComparer.Instance);
        return new([.. sandbox.Dummies.Select(d => new Enemy(d.CurrentHp, d.Block, Powers(d)))],
            player.Creature.CurrentHp, player.Creature.MaxHp, player.Creature.Block, s?.Energy ?? 0, s?.Stars ?? 0,
            player.PotionSlots.Count(potion => potion is not null), player.PotionSlots.Count, player.Gold, player.Deck.Cards.Count,
            player.Deck.Cards.Count(card => card.IsUpgraded), player.Relics.Count,
            Set(s?.Hand), Set(s?.DrawPile), Set(s?.DiscardPile), Set(s?.ExhaustPile), cards,
            new(cards.Where(card => card.IsUpgraded), ReferenceEqualityComparer.Instance),
            new(sandbox.Generated, ReferenceEqualityComparer.Instance), Powers(player.Creature));
    }

    private sealed record Sandbox(CombatEngine Engine, CombatState Combat, Rooms.CombatRoom Room, Player Player, Creature[] Dummies,
        List<CardModel> Generated)
    {
        public static Sandbox Create()
        {
            var run = new SandboxRunState();
            var combat = new CombatState(run) { CardSelectionSource = new FirstCardsSelector() };
            Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
            run.AddPlayer(player);
            // Large pool at half HP: heals and below-half conditions show, and three enemy turns cannot kill.
            player.Creature.SetMaxHpInternal(ProbeHp);
            player.Creature.SetCurrentHpInternal(ProbeHp / 2);
            Creature[] dummies = [.. Enumerable.Range(0, 2).Select(_ =>
                combat.AddMonster((MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy))];
            var engine = new CombatEngine(combat);
            // Room-entry relics read the room's engine; the sandbox room is announced to listeners, never entered.
            var room = new Rooms.CombatRoom(() => ModelDb.Monster<TrainingDummy>());
            typeof(Rooms.CombatRoom).GetProperty(nameof(Rooms.CombatRoom.Engine))!.SetValue(room, engine);
            return new(engine, combat, room, player, dummies, []);
        }

        /// <summary>Plays fresh copies, each at full energy, so cost changes never stop the script.</summary>
        public async Task PlayEachAsync(CardModel canonical, Creature? target, int plays)
        {
            for (int i = 0; i < plays && Engine.IsInProgress; i++)
            {
                Player.PlayerCombatState!.Energy = ProbeEnergy;
                await PlayAsync(canonical, target);
            }
        }

        /// <summary>Plays a fresh copy of a scripted card; the copy itself is not counted as a created card.</summary>
        public async Task PlayAsync(CardModel canonical, Creature? target)
        {
            var card = (CardModel)canonical.MutableClone();
            card.AssignOwner(Player);
            Generated.Add(card);
            await CardPileCmd.Generate(Combat, card, PileType.Hand);
            await Engine.PlayCardAsync(Player, card, target);
        }
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

    /// <summary>Like RunState's dispatcher: the player's unmelted relics and potions, then the combat's listeners.</summary>
    private sealed class SandboxRunState : IRunState
    {
        private readonly List<Player> _players = [];
        public AscensionManager Ascension { get; } = new(0);
        public RunRngSet Rng { get; } = new(SandboxSeed);
        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;
        public Rooms.AbstractRoom? CurrentRoom => null;
        public void AddPlayer(Player player) => _players.Add(player);
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState)
        {
            foreach (Player player in _players)
            {
                foreach (RelicModel relic in player.Relics.ToArray())
                    if (!relic.IsMelted) yield return relic;
                foreach (PotionModel potion in player.PotionSlots.OfType<PotionModel>())
                    yield return potion;
            }
            foreach (AbstractModel listener in childCombatState?.IterateHookListeners() ?? [])
                yield return listener;
        }
    }
}
