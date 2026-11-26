using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Events.Display;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.TestSupport;

namespace Sts2Sim.Core.Tests.Events;

/// <summary>Plan 09-S-1128: the read-only event display data (<see cref="EventDisplay"/>).</summary>
[Collection("ModelDb")]
public sealed class EventDisplayTests : IDisposable
{
    public EventDisplayTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    /// <summary>
    /// Non-string vars that read the game's literal default on the event's first page of this fixture. Each is a CanonicalVars
    /// constant the game does not change during the event (so the default is the game's value) unless noted; the simulator
    /// neither displays it on that page nor stores it under the same name.
    /// </summary>
    private static readonly HashSet<string> AllowedDefaults =
    [
        // Shown one page later (the flower's next prizes) / only while that dish is served.
        "COLOSSAL_FLOWER.Prize2", "COLOSSAL_FLOWER.Prize3",
        "ENDLESS_CONVEYOR.GoldenFyshGold", "ENDLESS_CONVEYOR.ClamRollHeal", "ENDLESS_CONVEYOR.CaviarMaxHp",
        // Constants of the option texts that the simulator's visible state does not repeat.
        "FIELD_OF_MAN_SIZED_HOLES.Gold", "FIELD_OF_MAN_SIZED_HOLES.Cards",
        "MUSEUM_OF_TORTURE.MaxHp",
        "POTION_COURIER.FoulPotions",
        "SELF_HELP_BOOK.Enchantment1Amount", "SELF_HELP_BOOK.Enchantment2Amount", "SELF_HELP_BOOK.Enchantment3Amount",
        "SPIRIT_GRAFTER.RejectionHpLoss", "SPIRIT_GRAFTER.LetItInHealAmount",
        "STONE_OF_ALL_TIME.DrinkMaxHpGain", "STONE_OF_ALL_TIME.PushHpLoss", "STONE_OF_ALL_TIME.PushVigorousAmount",
        "SYMBIOTE.Cards",
        "TABLET_OF_TRUTH.SmashHPGain",
        "TEA_MASTER.BoneTeaCost", "TEA_MASTER.EmberTeaCost",
        "THERMAE_NONUPEIPE.ThermalBathsCost", "THERMAE_NONUPEIPE.DrinkHpLoss", "THERMAE_NONUPEIPE.DrinkGuiltyCount",
        "WATERFALL_OF_OBVIOUS_SECRETS.NegotiateCostKey", "WATERFALL_OF_OBVIOUS_SECRETS.BeatUpCurseCount",
        "WATERLOGGED_SCRIPTORIUM.Cards",
        "WUPPY_IN_TROUBLE.ImmediateWuppyHp", "WUPPY_IN_TROUBLE.CautiousWuppyHp",
        // Mad Science's rider numbers (card description constants).
        "TINKER_TIME.Damage", "TINKER_TIME.Block", "TINKER_TIME.SappingWeak", "TINKER_TIME.SappingVulnerable",
        "TINKER_TIME.ViolenceHits", "TINKER_TIME.ChokingDamage", "TINKER_TIME.EnergizedEnergy", "TINKER_TIME.WisdomCards",
        "TINKER_TIME.ExpertiseStrength", "TINKER_TIME.ExpertiseDexterity", "TINKER_TIME.CuriousReduction",
        "TINKER_TIME.energyPrefix",
        // Changed by the game, not computable: Trial's cosmetic entrant number (a roll not seeded by the run) and Wongo's
        // points (the save's progress, set after a purchase).
        "TRIAL.EntrantNumber",
        "WELCOME_TO_WONGOS.WongoPointAmount", "WELCOME_TO_WONGOS.RemainingWongoPointAmount",
        "WELCOME_TO_WONGOS.TotalWongoBadgeAmount",
    ];

    /// <summary>Vars resolved by matching a simulator field name (DecimalField / Field rules). A renamed field drops the
    /// var to another rule and turns this test red.</summary>
    private static readonly HashSet<string> NameMatched =
    [
        "ABYSSAL_BATHS.Damage", "DENSE_VEGETATION.Gold", "JUNGLE_MAZE_ADVENTURE.JoinForcesGold", "JUNGLE_MAZE_ADVENTURE.SoloGold",
        "SUNKEN_STATUE.Gold", "SUNKEN_TREASURY.LargeChestGold", "SUNKEN_TREASURY.SmallChestGold", "WUPPY_IN_TROUBLE.HpLoss",
    ];

    [Fact]
    public async Task DynamicVars_cover_every_listed_event()
    {
        var missing = new List<string>();
        var nameMatched = new HashSet<string>();
        var unexpected = new List<string>();
        var usedAllowed = new HashSet<string>();
        foreach (var (id, table) in EventDisplayTables.DynamicVars)
        {
            if (FindEventType(id) is not { } type) { missing.Add(id); continue; }
            var (run, ev) = await Setup(type, "event-display-" + id);
            string context = $"event={id}; seed=event-display-{id}";
            string before = RunCloneGraphSnapshot.Capture(run);

            var vars = EventDisplay.GetDynamicVars(ev, out var sources);
            Assert.True(table.Select(v => (v.Name, v.VarType)).SequenceEqual(vars.Select(v => (v.Name, v.VarType))),
                $"{context}; names/types differ from the table: {string.Join(",", vars.Select(v => v.Name))}");
            for (int i = 0; i < vars.Count; i++)
            {
                Assert.True(vars[i].IsString == (table[i].VarType == "StringVar"), $"{context}; var={vars[i].Name} IsString");
                if (sources[i] is EventDisplay.VarSource.DecimalField or EventDisplay.VarSource.Field) nameMatched.Add(id + "." + vars[i].Name);
                if (sources[i] != EventDisplay.VarSource.Default) continue;
                string key = id + "." + vars[i].Name;
                if (AllowedDefaults.Contains(key)) usedAllowed.Add(key);
                else unexpected.Add(key);
            }
            foreach (EventOption option in ev.CurrentOptions) EventDisplay.DescribeOption(ev, option);
            Assert.True(before == RunCloneGraphSnapshot.Capture(run), $"{context}; display data changed state or RNG.");
        }
        Assert.True(missing.Count == 0, $"event ids in the table without a simulator event: {string.Join(", ", missing)}");
        Assert.True(unexpected.Count == 0, $"vars falling back to the literal default (a renamed field or lost visible value?): {string.Join(", ", unexpected)}");
        Assert.True(nameMatched.SetEquals(NameMatched),
            $"vars resolved by field name differ; actual: {string.Join(", ", nameMatched.Order().Select(k => '"' + k + '"'))}");
        Assert.True(usedAllowed.SetEquals(AllowedDefaults),
            $"allowed defaults no longer used (remove them): {string.Join(", ", AllowedDefaults.Except(usedAllowed))}");
    }

    // One case per text-key rule (1–7), an Ancient relic option, Hungry for Mushrooms and Trial's DOUBLE_DOWN.
    [Theory]
    [InlineData("TABLET_OF_TRUTH", "DECIPHER", "DECIPHER", "TABLET_OF_TRUTH.pages.DECIPHER_1.options.DECIPHER", true, false, null)]   // 1 visible DescriptionKey
    [InlineData("RELIC_TRADER", "", "PROCEED", "PROCEED", false, false, null)]                                                       // 2 unlisted PROCEED
    [InlineData("ABYSSAL_BATHS", "", "IMMERSE", "ABYSSAL_BATHS.pages.INITIAL.options.IMMERSE", true, false, null)]                  // 3 same name
    [InlineData("ENDLESS_CONVEYOR", "", "OBSERVE_CHEF", "ENDLESS_CONVEYOR.pages.INITIAL.options.OBSERVE_CHEF", true, false, null)]  // 3 INITIAL preferred
    [InlineData("TRIAL", "ACCEPT", "MERCHANT_GUILTY", "TRIAL.pages.MERCHANT.options.GUILTY", true, false, null)]                    // 4 PAGE_OPTION
    [InlineData("COLOSSAL_FLOWER", "REACH_DEEPER", "REACH_DEEPER", "COLOSSAL_FLOWER.pages.REACH_DEEPER_1.options.REACH_DEEPER_2", true, false, null)] // 5 numbered, later page
    [InlineData("NEOW", "", "Kaleidoscope", "NEOW.pages.INITIAL.options.KALEIDOSCOPE", false, false, "KALEIDOSCOPE")]               // 6 relic option
    [InlineData("DOLL_ROOM", "TAKE_SOME_TIME", "BING_BONG", "DOLL_ROOM.pages.INITIAL.options.BING_BONG", false, false, null)]       // 7 fallback
    [InlineData("HUNGRY_FOR_MUSHROOMS", "", "BIG_MUSHROOM", "HUNGRY_FOR_MUSHROOMS.pages.INITIAL.options.BIG_MUSHROOM", true, false, "BIG_MUSHROOM")]
    [InlineData("TRIAL", "REJECT", "DOUBLE_DOWN", "TRIAL.pages.REJECT.options.DOUBLE_DOWN", true, true, null)]
    public async Task Option_text_keys_and_relics(string id, string path, string key, string textKey, bool listed, bool proceed, string? relic)
    {
        var (_, ev, option, context) = await Reach(id, "event-display-" + id, path, key, relics: id != "RELIC_TRADER");
        EventOptionDisplay d = EventDisplay.DescribeOption(ev, option);
        Assert.True(textKey == d.TextKey, $"{context}; TextKey {d.TextKey}");
        Assert.True(listed == d.TextKeyIsListed, $"{context}; TextKeyIsListed {d.TextKeyIsListed}");
        Assert.True(proceed == d.IsProceed, $"{context}; IsProceed {d.IsProceed}");
        Assert.True(relic == d.Relic?.Id.Entry, $"{context}; Relic {d.Relic?.Id.Entry}");
    }

    // One case per hover rule: Slippery Bridge's deck card, the per-option table (Tea Master, Lost Wisp, Lantern Key, Wuppy,
    // Thermae, Tinker Time type / rider / none), Dusty Tome's upgraded card, the visible contents (card at its upgrade level,
    // potion, relic) and the relic-tip table (card, potion).
    [Theory]
    [InlineData("SLIPPERY_BRIDGE", "event-display-SLIPPERY_BRIDGE", "", "OVERCOME", "CARD.STRIKE_SILENT+0")]
    [InlineData("TEA_MASTER", "event-display-TEA_MASTER", "", "TEA_OF_DISCOURTESY", "CARD.DAZED+0")]
    [InlineData("LOST_WISP", "event-display-LOST_WISP", "", "CLAIM", "RELIC.LOST_WISP")]
    [InlineData("THE_LANTERN_KEY", "event-display-THE_LANTERN_KEY", "", "KEEP_THE_KEY", "-")]
    [InlineData("WUPPY_IN_TROUBLE", "event-display-WUPPY_IN_TROUBLE", "", "HELP_IMMEDIATELY", "CARD.INJURY+0")]
    [InlineData("WUPPY_IN_TROUBLE", "event-display-WUPPY_IN_TROUBLE", "", "BE_CAUTIOUS", "-")]
    [InlineData("THERMAE_NONUPEIPE", "event-display-THERMAE_NONUPEIPE", "", "THERMAL_BATHS", "-")]
    [InlineData("TINKER_TIME", "event-display-TINKER_TIME", "", "CHOOSE_CARD_TYPE", "-")]
    [InlineData("TINKER_TIME", "event-display-TINKER_TIME", "CHOOSE_CARD_TYPE", "ATTACK", "CARD.MAD_SCIENCE+0")]
    [InlineData("TINKER_TIME", "event-display-TINKER_TIME", "CHOOSE_CARD_TYPE,ATTACK", "VIOLENCE", "CARD.MAD_SCIENCE+0")]
    [InlineData("DARV", "event-display-DARV", "", "DustyTome", "CARD.WRAITH_FORM+1")]
    [InlineData("BUGSLAYER", "event-display-BUGSLAYER", "", "SQUASH", "CARD.SQUASH+0")]
    [InlineData("DROWNING_BEACON", "event-display-DROWNING_BEACON", "", "BOTTLE", "POTION.GLOWWATER_POTION")]
    [InlineData("SUNKEN_STATUE", "event-display-SUNKEN_STATUE", "", "GRAB_SWORD", "RELIC.SWORD_OF_STONE")]
    [InlineData("TEZCATARA", "event-display-TEZCATARA", "", "BiiigHug", "CARD.SOOT+0")]
    [InlineData("NEOW", "event-display-NEOW-8", "", "NeowsSacrifice", "POTION.AMBERGRIS")]
    public async Task Hover_objects(string id, string seed, string path, string key, string expected)
    {
        var (_, ev, option, context) = await Reach(id, seed, path, key, relics: true);
        AbstractModel? hover = EventDisplay.DescribeOption(ev, option).HoverObject;
        string actual = hover switch
        {
            null => "-",
            CardModel c => $"{c.Id}+{c.CurrentUpgradeLevel}",
            _ => hover.Id.ToString(),
        };
        Assert.True(expected == actual, $"{context}; hover {actual}");
        if (ev is SlipperyBridge bridge) Assert.Same(bridge.RandomCardToLose, hover);
        if (hover is Sts2Sim.Core.Models.Cards.MadScience ms) Assert.Same(ev.Owner, ms.Owner);
    }

    private static Type? FindEventType(string id) => ContentRegistry.AllTypes.FirstOrDefault(t =>
        typeof(EventModel).IsAssignableFrom(t) && !t.IsAbstract && ModelDb.GetId(t).Entry == id);

    private static async Task<(RunState Run, EventModel Event, EventOption Option, string Context)> Reach(
        string id, string seed, string path, string key, bool relics)
    {
        Type type = FindEventType(id) ?? throw new InvalidOperationException($"no simulator event {id}");
        var (run, ev) = await Setup(type, seed, relics);
        string context = $"event={id}; seed={seed}; path=[{path}]; option={key}";
        foreach (string step in path.Split(',', StringSplitOptions.RemoveEmptyEntries))
            await ev.ChooseOption(ev.CurrentOptions.Single(o => o.Key == step));
        EventOption? option = ev.CurrentOptions.FirstOrDefault(o => o.Key == key);
        Assert.True(option != null, $"{context}; options: {string.Join(",", ev.CurrentOptions.Select(o => o.Key))}");
        return (run, ev, option!, context);
    }

    private static async Task<(RunState Run, EventModel Event)> Setup(Type type, string seed, bool relics = true)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        player.Gold = 1000;
        player.Creature.SetMaxHpInternal(101);
        player.Creature.SetCurrentHpInternal(40);
        player.AddPotionInternal((PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone());
        player.AddPotionInternal((PotionModel)ModelDb.Potion<SwiftPotion>().MutableClone());
        if (relics)
        {
            await RelicCmd.Obtain(ModelDb.Relic<Anchor>(), player);
            await RelicCmd.Obtain(ModelDb.Relic<BagOfPreparation>(), player);
            await RelicCmd.Obtain(ModelDb.Relic<HappyFlower>(), player);
        }
        var ev = (EventModel)ModelDb.Get(type).MutableClone();
        var room = new EventRoom(() => ev);
        run.PushRoom(room);
        await room.Enter(run);
        return (run, ev);
    }
}
