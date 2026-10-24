using System.Globalization;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Rewards;
using EventVisibleState = Sts2Sim.Core.Runs.VisibleDecisionMetadata;
using EventVisibleValue = Sts2Sim.Core.Runs.VisibleValue;
using EventVisibleValueKind = Sts2Sim.Core.Runs.VisibleValueKind;
using System.Text.Json;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.TestSupport;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class EventVisibleStateTests : IDisposable
{
    public EventVisibleStateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("AbyssalBaths")]
    [InlineData("Amalgamator")]
    [InlineData("AromaOfChaos")]
    [InlineData("BattlewornDummy")]
    [InlineData("BrainLeech")]
    [InlineData("Bugslayer")]
    [InlineData("ByrdonisNest")]
    [InlineData("ColorfulPhilosophers")]
    [InlineData("ColossalFlower")]
    [InlineData("CrystalSphere")]
    [InlineData("Darv")]
    [InlineData("DenseVegetation")]
    [InlineData("DollRoom")]
    [InlineData("DoorsOfLightAndDark")]
    [InlineData("DrowningBeacon")]
    [InlineData("EndlessConveyor")]
    [InlineData("FakeMerchant")]
    [InlineData("FieldOfManSizedHoles")]
    [InlineData("GraveOfTheForgotten")]
    [InlineData("HungryForMushrooms")]
    [InlineData("InfestedAutomaton")]
    [InlineData("JungleMazeAdventure")]
    [InlineData("LostWisp")]
    [InlineData("LuminousChoir")]
    [InlineData("MorphicGrove")]
    [InlineData("Neow")]
    [InlineData("Nonupeipe")]
    [InlineData("Orobas")]
    [InlineData("Pael")]
    [InlineData("PotionCourier")]
    [InlineData("PunchOff")]
    [InlineData("RanwidTheElder")]
    [InlineData("Reflections")]
    [InlineData("RelicTrader")]
    [InlineData("RoomFullOfCheese")]
    [InlineData("RoundTeaParty")]
    [InlineData("SapphireSeed")]
    [InlineData("SelfHelpBook")]
    [InlineData("SlipperyBridge")]
    [InlineData("SpiralingWhirlpool")]
    [InlineData("SpiritGrafter")]
    [InlineData("StoneOfAllTime")]
    [InlineData("SunkenStatue")]
    [InlineData("SunkenTreasury")]
    [InlineData("Symbiote")]
    [InlineData("TabletOfTruth")]
    [InlineData("Tanx")]
    [InlineData("TeaMaster")]
    [InlineData("Tezcatara")]
    [InlineData("TheFutureOfPotions")]
    [InlineData("TheLanternKey")]
    [InlineData("TheLegendsWereTrue")]
    [InlineData("ThisOrThat")]
    [InlineData("TinkerTime")]
    [InlineData("TrashHeap")]
    [InlineData("Trial")]
    [InlineData("UnrestSite")]
    [InlineData("Vakuu")]
    [InlineData("WarHistorianRepy")]
    [InlineData("WaterloggedScriptorium")]
    [InlineData("WelcomeToWongos")]
    [InlineData("Wellspring")]
    [InlineData("WhisperingHollow")]
    [InlineData("WoodCarvings")]
    [InlineData("ZenWeaver")]
    [InlineData("Inventory")]
    [InlineData("Map")]
    [InlineData("Reward")]
    [InlineData("RewardExtra")]
    [InlineData("RewardAlternative")]
    [InlineData("ShopStock")]
    [InlineData("ShopRemoval")]
    [InlineData("Rest")]
    [InlineData("CardManual")]
    [InlineData("CardAutomatic")]
    [InlineData("CardBundle")]
    [InlineData("CustomSphere")]
    [InlineData("CustomFake")]
    [InlineData("Locked")]
    [InlineData("Duplicate")]
    [InlineData("Legacy")]
    [InlineData("Counters")]
    public async Task VisibleState_MatchesDisplayedValues_AndPreservesExecution(string eventType)
    {
        if (IsScenario(eventType)) { await VerifyScenario(eventType); return; }
        if (!LegacyEvents.Contains(eventType)) { await VerifyEvent(eventType); return; }
        string context = $"event={eventType}; seed=visible-634-{eventType}";
        var control = await Setup(eventType);
        var observed = await Setup(eventType);
        EventModel ev = observed.Event;
        EventVisibleState initial = ev.GetVisibleState().Metadata;
        string frozenInitial = Serialize(initial);
        string before = Snapshot(observed.Run);
        Assert.Equal(frozenInitial, Serialize(ev.GetVisibleState().Metadata));
        Assert.True(before == Snapshot(observed.Run), $"{context}; initial projection changed state/RNG.");
        Assert.True(Snapshot(control.Run) == before, $"{context}; initial control/observed state differs.");
        Assert.All(initial.DisplayOptions, option => Assert.Contains(ev.CurrentOptions, active => active.Key == option.OptionKey));
        Assert.All(AllValues(initial), value =>
        {
            if (value.Kind == EventVisibleValueKind.Integer)
                Assert.Equal(int.Parse(value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture), value.Value);
        });

        // These are the source/localization contracts in plan #634, not names inferred from fields.
        string[] expectedNames = eventType switch
        {
            "AbyssalBaths" => ["Damage", "Heal", "MaxHp"],
            "CrystalSphere" => ["CurseTitle", "PaymentPlanCount", "UncoverFutureCost", "UncoverFutureProphesizeCount"],
            "DenseVegetation" => ["Gold", "Heal", "HpLoss"],
            "EndlessConveyor" => ["Gold", "Gold"],
            "FakeMerchant" => [],
            "JungleMazeAdventure" => ["JoinForcesGold", "SoloGold", "SoloHp"],
            "LostWisp" => ["Curse", "Gold", "Relic"],
            "LuminousChoir" => ["Gold"],
            "RelicTrader" => ["BottomRelicNew", "BottomRelicOwned", "MiddleRelicNew", "MiddleRelicOwned", "TopRelicNew", "TopRelicOwned"],
            "SpiralingWhirlpool" => ["Heal"],
            "SunkenStatue" => ["Gold", "HpLoss", "Relic"],
            "SunkenTreasury" => ["LargeChestGold", "SmallChestGold"],
            "ThisOrThat" => ["Curse", "Gold", "HpLoss"],
            "WhisperingHollow" => ["Gold", "HpLoss"],
            _ => throw new InvalidOperationException(eventType),
        };
        if (eventType == "EndlessConveyor")
        {
            string dish = ev.CurrentOptions[0].Key;
            expectedNames = dish == "CAVIAR" ? ["CaviarMaxHp", "Gold", "Gold"]
                : dish == "CLAM_ROLL" ? ["ClamRollHeal", "Gold", "Gold"] : ["Gold", "Gold"];
        }
        Assert.True(expectedNames.Order().SequenceEqual(AllValues(initial).Where(v => expectedNames.Contains(v.Name)).Select(v => v.Name).Order()),
            $"{context}; displayed variable names differ: {string.Join(",", AllValues(initial).Select(v => v.Name))}.");
        switch (eventType)
        {
            case "AbyssalBaths":
                AssertValue(initial, "IMMERSE", "Damage", "3");
                AssertValue(initial, "IMMERSE", "MaxHp", "2");
                AssertValue(initial, "ABSTAIN", "Heal", "10");
                break;
            case "CrystalSphere":
                AssertValue(initial, "PAYMENT_PLAN", "CurseTitle", "DEBT", EventVisibleValueKind.ContentId);
                AssertValue(initial, "PAYMENT_PLAN", "PaymentPlanCount", "6");
                AssertValue(initial, "UNCOVER_FUTURE", "UncoverFutureProphesizeCount", "3");
                Assert.Equal(((CrystalSphere)ev).UncoverFutureCost.ToString(CultureInfo.InvariantCulture), Value(initial, "UNCOVER_FUTURE", "UncoverFutureCost").Value);
                break;
            case "DenseVegetation":
                AssertValue(initial, "TRUDGE_ON", "HpLoss", "8");
                AssertValue(initial, "REST", "Heal", "45");
                // A later MaxHp change does not refresh the native CalculateVars display snapshot.
                control.Player.Creature.SetMaxHpInternal(111);
                observed.Player.Creature.SetMaxHpInternal(111);
                Assert.Equal(frozenInitial, Serialize(ev.GetVisibleState().Metadata));
                break;
            case "EndlessConveyor":
                Assert.Equal(new EventVisibleValue("Gold", EventVisibleValueKind.Integer, "40"), Assert.Single(initial.ContextValues));
                string dishKey = ev.CurrentOptions[0].Key;
                AssertValue(initial, dishKey, "Gold", "40");
                if (dishKey == "CAVIAR") AssertValue(initial, dishKey, "CaviarMaxHp", "4");
                if (dishKey == "CLAM_ROLL") AssertValue(initial, dishKey, "ClamRollHeal", "10");
                break;
            case "FakeMerchant":
                var merchant = (FakeMerchant)ev;
                Assert.False(merchant.IsInventoryOpen);
                merchant.IsInventoryOpen = true;
                ((FakeMerchant)control.Event).IsInventoryOpen = true;
                Assert.Empty(AllValues(merchant.GetVisibleState().Metadata));
                break;
            case "JungleMazeAdventure":
                AssertValue(initial, "SOLO_QUEST", "SoloHp", "18");
                Assert.InRange(Integer(initial, "SOLO_QUEST", "SoloGold"), 135, 164);
                Assert.InRange(Integer(initial, "JOIN_FORCES", "JoinForcesGold"), 35, 64);
                break;
            case "LostWisp":
                AssertValue(initial, "CLAIM", "Curse", "DECAY", EventVisibleValueKind.ContentId);
                AssertValue(initial, "CLAIM", "Relic", "LOST_WISP", EventVisibleValueKind.ContentId);
                break;
            case "RelicTrader":
                var trader = (RelicTrader)ev;
                string[] positions = ["Top", "Middle", "Bottom"];
                for (int i = 0; i < 3; i++)
                {
                    string key = ev.CurrentOptions[i].Key;
                    AssertValue(initial, key, positions[i] + "RelicOwned", trader.OwnedRelics[i].Id.Entry, EventVisibleValueKind.ContentId);
                    AssertValue(initial, key, positions[i] + "RelicNew", trader.NewRelics[i].Id.Entry, EventVisibleValueKind.ContentId);
                }
                break;
            case "SpiralingWhirlpool": AssertValue(initial, "DRINK", "Heal", "33"); break;
            case "SunkenStatue":
                AssertValue(initial, "DIVE_INTO_WATER", "HpLoss", "7");
                AssertValue(initial, "GRAB_SWORD", "Relic", "SWORD_OF_STONE", EventVisibleValueKind.ContentId);
                break;
            case "ThisOrThat":
                AssertValue(initial, "PLAIN", "HpLoss", "6");
                AssertValue(initial, "ORNATE", "Curse", "CLUMSY", EventVisibleValueKind.ContentId);
                break;
            case "WhisperingHollow": AssertValue(initial, "HUG", "HpLoss", "9"); break;
        }

        if (eventType is "DenseVegetation" or "EndlessConveyor")
        {
            foreach (RunState clone in new[] { observed.Run.CloneExact(), observed.Run.CloneReseeded(634) })
                Assert.Equal(Serialize(ev.GetVisibleState().Metadata), Serialize(Assert.IsType<EventRoom>(clone.CurrentRoom).Event.GetVisibleState().Metadata));
        }
        int goldBefore = observed.Player.Gold;
        string[] script = eventType switch
        {
            "AbyssalBaths" => ["IMMERSE", "LINGER", "EXIT_BATHS"],
            "CrystalSphere" => ["UNCOVER_FUTURE"],
            "DenseVegetation" => ["REST", "FIGHT"],
            "EndlessConveyor" => [ev.CurrentOptions[0].Key, "LEAVE"],
            "FakeMerchant" => [],
            "JungleMazeAdventure" => ["SOLO_QUEST"],
            "LostWisp" => ["SEARCH"],
            "LuminousChoir" => ["OFFER_TRIBUTE"],
            "RelicTrader" => ["TOP"],
            "SpiralingWhirlpool" => ["DRINK"],
            "SunkenStatue" => ["DIVE_INTO_WATER"],
            "SunkenTreasury" => ["FIRST_CHEST"],
            "ThisOrThat" => ["PLAIN"],
            "WhisperingHollow" => ["GOLD"],
            _ => throw new InvalidOperationException(eventType),
        };
        foreach (string key in script)
        {
            Assert.Equal(control.Event.CurrentOptions.Select(o => (o.Key, o.IsLocked)), ev.CurrentOptions.Select(o => (o.Key, o.IsLocked)));
            string stable = Snapshot(observed.Run);
            _ = ev.GetVisibleState().Metadata;
            _ = ev.GetVisibleState().Metadata;
            Assert.True(stable == Snapshot(observed.Run), $"{context}; before {key}: projection changed state/RNG.");
            await control.Event.ChooseOption(control.Event.CurrentOptions.Single(o => o.Key == key));
            await ev.ChooseOption(ev.CurrentOptions.Single(o => o.Key == key));
            Assert.True(Snapshot(control.Run) == Snapshot(observed.Run), $"{context}; after {key}: control/observed state differs.");
            Assert.Equal(frozenInitial, Serialize(initial));
            if (eventType == "AbyssalBaths" && key != "EXIT_BATHS")
                AssertValue(ev.GetVisibleState().Metadata, "LINGER", "Damage", key == "IMMERSE" ? "4" : "5");
            if (eventType == "DenseVegetation" && key == "REST") Assert.Empty(AllValues(ev.GetVisibleState().Metadata));
            if (eventType == "CrystalSphere") Assert.Empty(AllValues(ev.GetVisibleState().Metadata));
            if (eventType == "EndlessConveyor" && key != "LEAVE")
            {
                EventVisibleValue previous = Assert.Single(AllValues(ev.GetVisibleState().Metadata).Where(v => v.Name == "LastDishTitle"));
                Assert.Equal(EventVisibleValueKind.LocalizationKey, previous.Kind);
                Assert.Contains(key, previous.Value, StringComparison.Ordinal);
                Assert.DoesNotContain(AllValues(ev.GetVisibleState().Metadata), v => v.Name == "CurrentDishTitle");
                foreach (RunState clone in new[] { observed.Run.CloneExact(), observed.Run.CloneReseeded(635) })
                    Assert.Equal(Serialize(ev.GetVisibleState().Metadata), Serialize(Assert.IsType<EventRoom>(clone.CurrentRoom).Event.GetVisibleState().Metadata));
            }
        }
        if (eventType == "FakeMerchant")
        {
            await ((FakeMerchant)control.Event).Leave();
            await ((FakeMerchant)ev).Leave();
            Assert.True(Snapshot(control.Run) == Snapshot(observed.Run), $"{context}; after merchant leave: control/observed state differs.");
        }
        if (eventType != "CrystalSphere")
        {
            Assert.True(ev.IsFinished);
            Assert.Empty(AllValues(ev.GetVisibleState().Metadata));
            Assert.Empty(ev.GetVisibleState().Metadata.DisplayOptions);
        }
        (string? option, string? name, int sign) = eventType switch
        {
            "JungleMazeAdventure" => ("SOLO_QUEST", "SoloGold", 1),
            "LostWisp" => ("SEARCH", "Gold", 1),
            "LuminousChoir" => ("OFFER_TRIBUTE", "Gold", -1),
            "SunkenStatue" => ("DIVE_INTO_WATER", "Gold", 1),
            "SunkenTreasury" => ("FIRST_CHEST", "SmallChestGold", 1),
            "ThisOrThat" => ("PLAIN", "Gold", 1),
            "WhisperingHollow" => ("GOLD", "Gold", -1),
            _ => (null, null, 0),
        };
        if (option is not null) Assert.Equal(sign * Integer(initial, option, name!), observed.Player.Gold - goldBefore);
    }


    private static readonly string[] LegacyEvents = ["AbyssalBaths", "CrystalSphere", "DenseVegetation", "EndlessConveyor", "FakeMerchant", "JungleMazeAdventure", "LostWisp", "LuminousChoir", "RelicTrader", "SpiralingWhirlpool", "SunkenStatue", "SunkenTreasury", "ThisOrThat", "WhisperingHollow"];
    private static bool IsScenario(string name) => name is "Inventory" or "Map" or "Reward" or "RewardExtra" or "RewardAlternative" or "ShopStock" or "ShopRemoval" or "Rest" or "CardManual" or "CardAutomatic" or "CardBundle" or "CustomSphere" or "CustomFake" or "Locked" or "Duplicate" or "Legacy" or "Counters";

    private static async Task VerifyEvent(string name)
    {
        var initial = await Setup(name);
        Assert.NotEmpty(initial.Event.CurrentOptions);
        AssertSnapshot(initial.Run, initial.Event);
        AssertSourceExamples(name, initial.Event.GetVisibleState().Metadata);
        if (name == "Darv")
        {
            var regent = await Setup("Darv", ModelDb.Character<Regent>());
            var tome = (DustyTome)ModelDb.Relic<DustyTome>().MutableClone();
            tome.AncientCard = ModelDb.GetId<TheSealedThrone>();
            string beforePreview = Snapshot(regent.Run);
            // Exercise the actual Ancient offered-content producer with an explicitly configured
            // public source item; no seed search or SetupForPlayer reward draw is needed.
            var offer = (EventOption)typeof(AncientEventModel).GetMethod("RelicOption",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                binder: null, types: [typeof(RelicModel), typeof(string)], modifiers: null)!
                .Invoke(regent.Event, [tome, "DustyTome"])!;
            VisibleContent preview = Assert.Single(offer.OfferedContent!.Children, child => child.ContentId == "THE_SEALED_THRONE");
            Assert.Contains(VisibleValue.Int("EnergyCost", 0), preview.Values);
            Assert.Contains(VisibleValue.Int("StarCost", 3), preview.Values);
            Assert.Contains(VisibleValue.Scope("Role", "AncientCard"), preview.Values);
            Assert.Equal(beforePreview, Snapshot(regent.Run));
        }
        // Each offered branch is executed once on a fresh control/observed pair. Further pages
        // are followed without combat simulation; forced combat/reward boundaries remain explicit.
        int branches = initial.Event.CurrentOptions.Count;
        for (int branch = 0; branch < branches; branch++)
        {
            var control = await Setup(name);
            var observed = await Setup(name);
            control.Run.ConfigureCardSelectionSource(new SelectionProbe());
            observed.Run.ConfigureCardSelectionSource(new SelectionProbe());
            Assert.Equal(Snapshot(control.Run), Snapshot(observed.Run));
            for (int page = 0; page < 10 && !observed.Event.IsFinished; page++)
            {
                AssertSnapshot(observed.Run, observed.Event);
                Assert.Equal(control.Event.CurrentOptions.Select(o => (o.Key, o.IsLocked)), observed.Event.CurrentOptions.Select(o => (o.Key, o.IsLocked)));
                if (observed.Event.IsAwaitingForcedCombat || observed.Event.HasPendingRewardOffers) break;
                EventOption[] offered = observed.Event.CurrentOptions.ToArray();
                int selected = page == 0 ? branch : Array.FindIndex(offered, o => o.IsEnabled);
                if (page > 0 && name is "SlipperyBridge" or "TabletOfTruth" or "ColossalFlower")
                {
                    int continuation = Array.FindIndex(offered, o => o.IsEnabled && (o.Key.StartsWith("HOLD_ON", StringComparison.Ordinal) || o.Key == "DECIPHER" || o.Key == "REACH_DEEPER"));
                    if (continuation >= 0) selected = continuation;
                }
                if (selected < 0 || !offered[selected].IsEnabled) break;
                string frozen = Serialize(observed.Event.GetVisibleState().Metadata);
                EventVisibleState retained = observed.Event.GetVisibleState().Metadata;
                await control.Event.ChooseOption(control.Event.CurrentOptions[selected]);
                await observed.Event.ChooseOption(offered[selected]);
                Assert.Equal(Snapshot(control.Run), Snapshot(observed.Run));
                Assert.Equal(frozen, Serialize(retained));
            }
        }
    }


    private static void AssertSourceExamples(string name, VisibleDecisionMetadata state)
    {
        // Source values from approved plan #637 section 4, release 41cef1ea. These are displayed
        // constants, never recomputed by executing a candidate or querying its projection.
        (string Key, string Name, string Value)? example = name switch
        {
            "Amalgamator" => ("COMBINE_STRIKES", "RemoveCount", "2"),
            "AromaOfChaos" => ("LET_GO", "TransformCount", "1"),
            "BattlewornDummy" => ("SETTING_1", "Setting1Hp", "75"),
            "BrainLeech" => ("RIP", "RipHpLoss", "5"),
            "Bugslayer" => ("EXTERMINATION", "ObtainCount", "1"),
            "ByrdonisNest" => ("EAT", "MaxHp", "7"),
            "ColossalFlower" => ("EXTRACT_CURRENT_PRIZE", "Prize1", "35"),
            "DoorsOfLightAndDark" => ("LIGHT", "Cards", "2"),
            "DrowningBeacon" => ("CLIMB", "HpLoss", "13"),
            "MorphicGrove" => ("LONER", "MaxHp", "5"),
            "Reflections" => ("TOUCH_A_MIRROR", "UpgradeCount", "4"),
            "SpiritGrafter" => ("LET_IT_IN", "Heal", "25"),
            "TabletOfTruth" => ("SMASH", "Heal", "20"),
            "TrashHeap" => ("GRAB", "Gold", "100"),
            "UnrestSite" => ("KILL", "MaxHpLoss", "8"),
            "WarHistorianRepy" => ("UNLOCK_CHEST", "PotionCount", "2"),
            "WaterloggedScriptorium" => ("BLOODY_INK", "MaxHp", "6"),
            "DollRoom" => ("TAKE_SOME_TIME", "TakeTimeHpLoss", "5"),
            "FieldOfManSizedHoles" => ("RESIST", "RemoveCount", "2"),
            "GraveOfTheForgotten" => ("ACCEPT", "RelicCount", "1"),
            "HungryForMushrooms" => ("BIG_MUSHROOM", "MaxHp", "20"),
            "InfestedAutomaton" => ("TOUCH_CORE", "EnergyCost", "0"),
            "PotionCourier" => ("GRAB_POTIONS", "Count", "3"),
            "PunchOff" => ("NAB", "CurseCount", "1"),
            "RanwidTheElder" => ("GOLD", "Gold", "100"),
            "RoomFullOfCheese" => ("GORGE", "OfferCount", "8"),
            "RoundTeaParty" => ("PICK_FIGHT", "Damage", "11"),
            "SapphireSeed" => ("EAT", "Heal", "9"),
            "SelfHelpBook" => ("READ_THE_BACK", "Amount", "2"),
            "SlipperyBridge" => ("HOLD_ON_0", "HpLoss", "3"),
            "StoneOfAllTime" => ("PUSH", "HpLoss", "6"),
            "Symbiote" => ("APPROACH", "DamageBonusPercent", "50"),
            "TeaMaster" => ("BONE_TEA", "GoldCost", "50"),
            "TheLanternKey" => ("RETURN_THE_KEY", "Gold", "100"),
            "TheLegendsWereTrue" => ("SLOWLY_FIND_AN_EXIT", "Damage", "8"),
            "TinkerTime" => ("CHOOSE_CARD_TYPE", "CardCount", "1"),
            "WelcomeToWongos" => ("BARGAIN_BIN", "BargainBinCost", "100"),
            "Wellspring" => ("BOTTLE", "PotionCount", "1"),
            "WoodCarvings" => ("BIRD", "TransformCount", "1"),
            "ZenWeaver" => ("BREATHING_TECHNIQUES", "BreathingTechniquesCost", "50"),
            _ => null,
        };
        if (example is { } expected)
            AssertValue(state, expected.Key, expected.Name, expected.Value);
        if (name == "ColorfulPhilosophers")
        {
            Assert.Contains(VisibleValue.Int("StatueCount", 3), state.ContextValues);
            Assert.All(state.DisplayOptions, option => Assert.Contains(VisibleValue.Int("Cards", 3), option.Values));
        }
        if (name == "Trial") Assert.Contains("G1.TrialNarrative", state.CoverageGapIds);
        if (name == "DrowningBeacon")
        {
            // 41cef1ea uses HpLossVar in the text and ThatDecreasesMaxHp for its scope.
            Assert.Contains(VisibleValue.Scope("LossKind", "MaxHp"), Assert.Single(state.DisplayOptions, option => option.OptionKey == "CLIMB").Values);
        }
    }

    private static void AssertSnapshot(RunState run, EventModel ev)
    {
        string before = Snapshot(run);
        VisibleDecisionSnapshot snapshot = ev.GetVisibleState();
        string frozen = Serialize(snapshot.Metadata);
        Assert.Equal(frozen, Serialize(ev.GetVisibleState().Metadata));
        Assert.Equal(before, Snapshot(run));
        Assert.Equal(ev.CurrentOptions.Count, snapshot.Metadata.DisplayOptions.Length);
        Assert.DoesNotContain(snapshot.Metadata.CoverageGapIds, gap => gap.StartsWith("Unsupported", StringComparison.Ordinal));
        for (int i = 0; i < ev.CurrentOptions.Count; i++)
        {
            EventOption original = ev.CurrentOptions[i];
            VisibleOption row = snapshot.DescribeCandidate(original);
            Assert.Same(snapshot.Metadata.DisplayOptions[i], row);
            Assert.Equal(original.Key, row.OptionKey);
            Assert.Equal(original.IsEnabled, row.IsEnabled);
            Assert.Throws<ArgumentException>(() => snapshot.DescribeCandidate(new EventOption(original.Key, () => Task.CompletedTask)));
        }
        if (ev is AncientEventModel && !ev.IsFinished)
        {
            Assert.All(snapshot.Metadata.DisplayOptions, option => Assert.NotEmpty(option.Contents));
            var displayedContents = new Queue<VisibleContent>(snapshot.Metadata.DisplayOptions.SelectMany(option => option.Contents));
            while (displayedContents.TryDequeue(out VisibleContent? content))
            {
                if (content.Values.Contains(VisibleValue.Scope("Role", "AncientCard")))
                {
                    Assert.Single(content.Values, value => value.Name == "Rarity");
                    Assert.Single(content.Values, value => value.Name == "EnergyCost");
                    Assert.Single(content.Values, value => value.Name == "StarCost");
                }
                foreach (VisibleContent child in content.Children) displayedContents.Enqueue(child);
            }
        }
        else if (!ev.IsFinished && ev.CurrentOptions.Count > 0 && !ev.IsAwaitingForcedCombat && !ev.HasPendingRewardOffers)
        {
            // Empty continuation pages have no extra numeric semantics in the approved source table.
            string[] emptyKeys = ["ACCEPT", "REJECT", "FIGHT", "CONTINUE_FIGHT", "PROCEED", "GIVE_UP", "LEAVE", "EXIT_BATHS"];
            Assert.All(snapshot.Metadata.DisplayOptions.Where(o => o.IsEnabled && !emptyKeys.Contains(o.OptionKey)), row =>
                Assert.True(row.Values.Length + row.Contents.Length > 0, $"{ev.Id}/{row.OptionKey}: current displayed semantics missing."));
        }
    }

    private static async Task VerifyScenario(string scenario)
    {
        if (scenario == "Inventory")
        {
            Type[] events = ContentRegistry.AllTypes.Where(t => typeof(EventModel).IsAssignableFrom(t) && !t.IsAbstract).ToArray();
            Assert.Equal(65, events.Length);
            Assert.Equal(57, events.Count(t => !typeof(AncientEventModel).IsAssignableFrom(t)));
            Assert.Equal(8, events.Count(t => typeof(AncientEventModel).IsAssignableFrom(t)));
            Assert.False(typeof(EventModel).IsAssignableFrom(typeof(ForcedCombatOutcome)));
            Assert.Equal(new ForcedCombatOutcome(true, false), new ForcedCombatOutcome(Victory: true, TimedOut: false));
            return;
        }
        var setup = await Setup("AbyssalBaths");
        RunState run = setup.Run;
        Player player = setup.Player;
        CardModel[] cards = player.Deck.Cards.Take(3).ToArray();
        VisibleDecisionSnapshot projection;
        object[] bindings;
        switch (scenario)
        {
            case "Map":
                MapPoint[] points = [new(2, 3) { PointType = MapPointType.Unknown }, new(4, 3) { PointType = MapPointType.RestSite }];
                points[0].AddChildPoint(new MapPoint(3, 4) { PointType = MapPointType.Elite });
                projection = Pure(run, () => VisibleDecisionProjection.ForMap(run, points));
                bindings = points;
                Assert.Contains(VisibleValue.Scope("PointType", "Unknown"), projection.DescribeCandidate(points[0]).Values);
                Assert.Contains(VisibleValue.Scope("Child", "3,4"), projection.DescribeCandidate(points[0]).Values);
                Assert.DoesNotContain(AllValues(projection.Metadata), value => value.Name.Contains("Encounter", StringComparison.Ordinal));
                break;
            case "Reward":
            case "RewardExtra":
            case "RewardAlternative":
                var gold = new GoldReward(42, player);
                var extra = new CardRemovalReward(player);
                var rewards = RewardsSet.CreateCustom(player, gold: gold, extraRewards: [extra]);
                var alternative = new CardRewardAlternative("fixture_alternative", () => throw new InvalidOperationException("Projection executed selection"), () => throw new InvalidOperationException("Projection queried alternative"));
                RewardDecision choice = scenario == "RewardExtra" ? new RewardDecision.ResolveExtra(extra, cards[0])
                    : scenario == "RewardAlternative" ? new RewardDecision.SelectCardAlternative(rewards.Card, alternative)
                    : new RewardDecision.TakeGold();
                RewardDecision[] rewardChoices = [choice, new RewardDecision.Done()];
                projection = Pure(run, () => VisibleDecisionProjection.ForRewards(rewards, rewardChoices));
                bindings = rewardChoices;
                if (scenario == "Reward") Assert.Contains(VisibleValue.Int("Amount", 42), projection.DescribeCandidate(choice).Values);
                if (scenario == "RewardExtra") Assert.Equal(cards[0].Id.Entry, Assert.Single(projection.DescribeCandidate(choice).Contents).ContentId);
                if (scenario == "RewardAlternative") Assert.Contains(VisibleValue.Id("AlternativeId", "fixture_alternative"), projection.DescribeCandidate(choice).Values);
                break;
            case "ShopStock":
            case "ShopRemoval":
            case "Locked":
                MerchantInventory inventory = MerchantInventory.Generate(player);
                if (scenario == "Locked") player.Gold = 0;
                ShopDecision[] shopChoices = scenario == "Locked" ? [new ShopDecision.Leave()]
                    : scenario == "ShopRemoval" ? [new ShopDecision.BuyCardRemoval(cards[0]), new ShopDecision.Leave()]
                    : [new ShopDecision.BuyCard(inventory.Cards[0]), new ShopDecision.BuyRelic(inventory.Relics[0]), new ShopDecision.BuyPotion(inventory.Potions[0]), new ShopDecision.Leave()];
                projection = Pure(run, () => VisibleDecisionProjection.ForShop(inventory, player, shopChoices));
                bindings = shopChoices;
                Assert.Equal(inventory.Cards.Count + inventory.Relics.Count + inventory.Potions.Count + 1 + shopChoices.Length, projection.Metadata.DisplayOptions.Length);
                Assert.Contains(projection.Metadata.DisplayOptions, row => row.Values.Contains(VisibleValue.Int("Price", inventory.Cards[0].Price)));
                if (scenario == "ShopRemoval") Assert.Equal(cards[0].Id.Entry, Assert.Single(projection.DescribeCandidate(shopChoices[0]).Contents).ContentId);
                if (scenario == "Locked")
                {
                    Assert.All(projection.Metadata.DisplayOptions.Where(row => row.OptionKey.StartsWith("stock:", StringComparison.Ordinal)), row => Assert.False(row.IsEnabled));
                        // Regenerate via a fresh fixture, because lock state is captured by the current page.
                    var lockedRun = new RunState("visible-locked", new Overgrowth());
                    var lockedPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), lockedRun); lockedRun.AddPlayer(lockedPlayer); lockedPlayer.Gold = 0;
                    var zen = (EventModel)ModelDb.Event<ZenWeaver>().MutableClone(); zen.AssignOwner(lockedPlayer); zen.BeginEvent(lockedRun);
                    Assert.Contains(zen.CurrentOptions, o => o.IsLocked);
                    Assert.All(zen.GetVisibleState().Metadata.DisplayOptions.Where(o => !o.IsEnabled), row => Assert.Empty(row.Values));
                    // Native Smith remains visible without upgrade targets. MeatCleaver's Cook
                    // remains visible with fewer than two removable cards; neither is a legal action.
                    foreach (CardModel card in lockedPlayer.Deck.Cards.Skip(1).ToArray())
                        await CardPileCmd.RemoveFromDeck(lockedPlayer, card);
                    CardCmd.Upgrade(Assert.Single(lockedPlayer.Deck.Cards));
                    await RelicCmd.Obtain((RelicModel)ModelDb.Relic<MeatCleaver>().MutableClone(), lockedPlayer);
                    var disabledRoom = new RestSiteRoom(); lockedRun.PushRoom(disabledRoom); await disabledRoom.Enter(lockedRun);
                    string beforeDisplayRead = Snapshot(lockedRun);
                    Assert.Empty(disabledRoom.GetDisplayedOptionIds(lockedPlayer));
                    Assert.Equal(beforeDisplayRead, Snapshot(lockedRun));
                    IReadOnlyList<RestSiteDecision> legal = disabledRoom.GetAvailableDecisions(lockedRun, lockedPlayer);
                    Assert.DoesNotContain(legal, candidate => candidate is RestSiteDecision.Smith or RestSiteDecision.Cook);
                    var disabledSnapshot = Pure(lockedRun, () => VisibleDecisionProjection.ForRestSite(lockedPlayer, legal));
                    VisibleOption disabledSmith = Assert.Single(disabledSnapshot.Metadata.DisplayOptions, row => row.OptionKey == "smith");
                    VisibleOption disabledCook = Assert.Single(disabledSnapshot.Metadata.DisplayOptions, row => row.OptionKey == "cook");
                    Assert.False(disabledSmith.IsEnabled); Assert.False(disabledCook.IsEnabled);
                    Assert.Equal(new[] { VisibleValue.Key("Description", "rest_site_ui:OPTION_SMITH.descriptionDisabled") }, disabledSmith.Values);
                    Assert.Equal(new[] { VisibleValue.Key("Description", "rest_site_ui:OPTION_COOK.descriptionDisabled"), VisibleValue.Int("Cards", 2) }, disabledCook.Values);
                    Assert.DoesNotContain(disabledSmith.Values, value => value.Name == "Count");
                    Assert.DoesNotContain(disabledCook.Values, value => value.Name == "MaxHp");
                    for (int i = 0; i < legal.Count; i++)
                    {
                        Assert.Same(legal[i], disabledRoom.GetAvailableDecisions(lockedRun, lockedPlayer)[i]);
                        Assert.Same(disabledSnapshot.Metadata.DisplayOptions[i], disabledSnapshot.DescribeCandidate(legal[i]));
                    }
                    string capturedDisabled = Serialize(disabledSnapshot.Metadata);
                    RunState disabledClone = lockedRun.CloneExact();
                    Player clonePlayer = Assert.Single(disabledClone.Players);
                    var cloneRoom = Assert.IsType<RestSiteRoom>(disabledClone.CurrentRoom);
                    IReadOnlyList<RestSiteDecision> cloneLegal = cloneRoom.GetAvailableDecisions(disabledClone, clonePlayer);
                    Assert.Equal(capturedDisabled, Serialize(Pure(disabledClone, () => VisibleDecisionProjection.ForRestSite(clonePlayer, cloneLegal)).Metadata));
                    Assert.Equal(disabledRoom.GetDisplayedOptionIds(lockedPlayer), cloneRoom.GetDisplayedOptionIds(clonePlayer));
                    await cloneRoom.ResolveAsync(clonePlayer, Assert.Single(cloneLegal, candidate => candidate is RestSiteDecision.Heal));
                    Assert.Empty(cloneRoom.GetDisplayedOptionIds(clonePlayer));
                    Assert.Empty(Pure(disabledClone, () => VisibleDecisionProjection.ForRestSite(clonePlayer, cloneRoom.GetAvailableDecisions(disabledClone, clonePlayer))).Metadata.DisplayOptions);
                    Assert.Equal(capturedDisabled, Serialize(disabledSnapshot.Metadata));
                    await disabledRoom.Exit(lockedRun);
                    Assert.Empty(disabledRoom.GetDisplayedOptionIds(lockedPlayer));
                }
                break;
            case "Rest":
                foreach (Type type in new[] { typeof(RegalPillow), typeof(StoneHumidifier), typeof(TinyMailbox), typeof(DreamCatcher), typeof(Girya), typeof(PumpkinCandle) })
                    await RelicCmd.Obtain((RelicModel)ModelDb.Get(type).MutableClone(), player);
                RestSiteDecision[] rest = [new RestSiteDecision.Heal(), new RestSiteDecision.Smith(cards[0]), new RestSiteDecision.Cook(), new RestSiteDecision.Hatch(), new LiftRestSiteOption(), new KindleRestSiteOption(), new CloneRestSiteOption(), new DigRestSiteOption()];
                projection = Pure(run, () => VisibleDecisionProjection.ForRestSite(player, rest)); bindings = rest;
                Assert.Equal(8, projection.Metadata.DisplayOptions.Length);
                Assert.DoesNotContain(projection.Metadata.CoverageGapIds, gap => gap.StartsWith("Unsupported", StringComparison.Ordinal));
                Assert.Contains("G3.Modifier.NightTerrors", projection.Metadata.CoverageGapIds);
                Assert.Contains("G4.Multiplayer.Mend", projection.Metadata.CoverageGapIds);
                Assert.Contains(VisibleValue.Int("Heal", 45), projection.DescribeCandidate(rest[0]).Values);
                Assert.Equal(4, projection.DescribeCandidate(rest[0]).Contents.Length);
                Assert.Contains(VisibleValue.Int("Cards", 2), projection.DescribeCandidate(rest[2]).Values);
                Assert.Contains(VisibleValue.Int("MaxHp", 5), projection.DescribeCandidate(rest[2]).Values);
                Assert.Contains(VisibleValue.Int("LiftsLeft", 3), projection.DescribeCandidate(rest[4]).Values);
                var liveRestRoom = new RestSiteRoom(); run.PushRoom(liveRestRoom); await liveRestRoom.Enter(run);
                IReadOnlyList<RestSiteDecision> liveOptions = liveRestRoom.GetAvailableDecisions(run, player);
                var liveSnapshot = Pure(run, () => VisibleDecisionProjection.ForRestSite(player, liveOptions));
                for (int i = 0; i < liveOptions.Count; i++)
                {
                    Assert.Same(liveOptions[i], liveRestRoom.GetAvailableDecisions(run, player)[i]);
                    Assert.Same(liveSnapshot.Metadata.DisplayOptions[i], liveSnapshot.DescribeCandidate(liveOptions[i]));
                }
                RestSiteDecision smithAction = liveOptions.First(option => option is RestSiteDecision.Smith);
                await liveRestRoom.ResolveAsync(player, smithAction);
                Assert.DoesNotContain("smith", liveRestRoom.GetDisplayedOptionIds(player));
                IReadOnlyList<RestSiteDecision> remaining = liveRestRoom.GetAvailableDecisions(run, player);
                Assert.DoesNotContain(Pure(run, () => VisibleDecisionProjection.ForRestSite(player, remaining)).Metadata.DisplayOptions, row => row.OptionKey == "smith");
                RunState consumedClone = run.CloneExact();
                var consumedRoom = Assert.IsType<RestSiteRoom>(consumedClone.CurrentRoom);
                Player consumedPlayer = Assert.Single(consumedClone.Players);
                Assert.DoesNotContain("smith", consumedRoom.GetDisplayedOptionIds(consumedPlayer));
                Assert.DoesNotContain(Pure(consumedClone, () => VisibleDecisionProjection.ForRestSite(consumedPlayer, consumedRoom.GetAvailableDecisions(consumedClone, consumedPlayer))).Metadata.DisplayOptions, row => row.OptionKey == "smith");
                break;
            case "CardManual":
            case "CardAutomatic":
            case "CardBundle":
                await VerifySelection(run, player, cards, scenario); return;
            case "CustomSphere":
                var sphere = await Setup("CrystalSphere");
                await sphere.Event.ChooseOption(sphere.Event.CurrentOptions.Single(o => o.Key == "UNCOVER_FUTURE"));
                CustomEventDecision[] reveal = [new CustomEventDecision.RevealSphere(0, 0), new CustomEventDecision.RevealSphere(0, 0, false)];
                projection = Pure(sphere.Run, () => VisibleDecisionProjection.ForCustomEvent(sphere.Event, reveal)); bindings = reveal;
                Assert.Contains(VisibleValue.Int("DivinationCount", 3), projection.Metadata.ContextValues);
                Assert.Contains(VisibleValue.Int("Width", 11), projection.Metadata.ContextValues);
                Assert.Contains(VisibleValue.Scope("InformationBoundary", "CrystalSphereLayoutException"), projection.Metadata.ContextValues);
                Assert.DoesNotContain(projection.Metadata.ContextValues, value => value.Kind == VisibleValueKind.ContentId && value.Value != "DOUBT");
                break;
            case "CustomFake":
                var fake = await Setup("FakeMerchant");
                var merchant = (FakeMerchant)fake.Event;
                Assert.False(merchant.IsInventoryOpen);
                MerchantRelicEntry closedEntry = merchant.Inventory.Relics[0];
                CustomEventDecision buyClosed = new CustomEventDecision.BuyRelic(closedEntry);
                CustomEventDecision[] fakeChoices = [buyClosed, new CustomEventDecision.Leave()];
                var closed = Pure(fake.Run, () => VisibleDecisionProjection.ForCustomEvent(merchant, fakeChoices));
                VisibleOption closedBuy = closed.DescribeCandidate(buyClosed);
                Assert.Same(closed.Metadata.DisplayOptions[0], closedBuy);
                Assert.DoesNotContain(closedBuy.CoverageGapIds, gap => gap.StartsWith("Unsupported", StringComparison.Ordinal));
                Assert.DoesNotContain(closed.Metadata.CoverageGapIds, gap => gap.StartsWith("Unsupported", StringComparison.Ordinal));
                Assert.Contains(VisibleValue.Id("Relic", closedEntry.Relic.Id.Entry), closedBuy.Values);
                Assert.Contains(VisibleValue.Int("Price", closedEntry.Price), closedBuy.Values);
                Assert.Equal(closedEntry.Relic.Id.Entry, Assert.Single(closedBuy.Contents).ContentId);
                Assert.Contains(closed.Metadata.ContextValues, value => value.Name.EndsWith(".Price", StringComparison.Ordinal) && value.Value == closedEntry.Price.ToString(CultureInfo.InvariantCulture));
                merchant.IsInventoryOpen = true;
                projection = Pure(fake.Run, () => VisibleDecisionProjection.ForCustomEvent(merchant, fakeChoices)); bindings = fakeChoices;
                VisibleOption openBuy = projection.DescribeCandidate(buyClosed);
                Assert.Contains(VisibleValue.Id("Relic", closedEntry.Relic.Id.Entry), openBuy.Values);
                Assert.Contains(VisibleValue.Int("Price", closedEntry.Price), openBuy.Values);
                Assert.Equal(closedEntry.Relic.Id.Entry, Assert.Single(openBuy.Contents).ContentId);
                Assert.DoesNotContain(openBuy.CoverageGapIds, gap => gap.StartsWith("Unsupported", StringComparison.Ordinal));
                Assert.Equal(Serialize(closed.Metadata), Serialize(projection.Metadata));
                break;
            case "Duplicate":
                var future = await Setup("TheFutureOfPotions");
                Assert.Equal(3, future.Event.CurrentOptions.Count);
                Assert.All(future.Event.CurrentOptions, option => Assert.Equal("POTION", option.Key));
                var futureSnapshot = Pure(future.Run, () => future.Event.GetVisibleState());
                string[] potionIds = future.Player.PotionSlots.OfType<PotionModel>().Select(potion => potion.Id.Entry).ToArray();
                for (int i = 0; i < 3; i++)
                    Assert.Contains(VisibleValue.Id("PotionId", potionIds[i]), futureSnapshot.DescribeCandidate(future.Event.CurrentOptions[i]).Values);
                // Distinct actual offered objects with the same key must remain separately bound.
                var repeated = new[] { new EventOption("POTION", () => Task.CompletedTask), new EventOption("POTION", () => Task.CompletedTask) };
                var metadata = new VisibleDecisionMetadata([], [new("POTION", [VisibleValue.Int("Amount", 1)]), new("POTION", [VisibleValue.Int("Amount", 2)])]);
                projection = new(metadata, repeated); bindings = repeated;
                Assert.Contains(VisibleValue.Int("Amount", 1), projection.DescribeCandidate(repeated[0]).Values);
                Assert.Contains(VisibleValue.Int("Amount", 2), projection.DescribeCandidate(repeated[1]).Values);
                Assert.Equal(2, projection.Metadata.DisplayOptions.Length);
                AssertSnapshot(future.Run, future.Event);
                break;
            case "Legacy":
                var genetic = (GeneticAlgorithm)ModelDb.Card<GeneticAlgorithm>().MutableClone();
                Assert.Null(VisibleContentProjection.LegacyCard(genetic).NumericDeltas);
                Assert.Contains(VisibleValue.Int("Block", 1), VisibleContentProjection.ForCard(genetic).Values);
                Assert.Equal("GENETIC_ALGORITHM", VisibleContentProjection.LegacyCard(genetic).Id);
                return;
            case "Counters":
                var counter = (PenNib)ModelDb.Relic<PenNib>().MutableClone();
                typeof(PenNib).GetField("_attacksPlayed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(counter, 17);
                VisibleContent badge = VisibleContentProjection.ForRelic(counter);
                Assert.Contains(VisibleValue.Condition("ShowCounter", "true"), badge.Values);
                Assert.Contains(VisibleValue.Int("DisplayAmount", 7), badge.Values);
                var ga = (GeneticAlgorithm)ModelDb.Card<GeneticAlgorithm>().MutableClone();
                typeof(GeneticAlgorithm).GetField("_currentBlock", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(ga, 19);
                Assert.Contains(VisibleValue.Int("Block", 19), VisibleContentProjection.ForCard(ga).Values);
                Assert.DoesNotContain(VisibleContentProjection.ForCard(ga).CoverageGapIds, gap => gap.StartsWith("G2.Instance", StringComparison.Ordinal));
                var mad = (MadScience)ModelDb.Card<MadScience>().MutableClone();
                mad.Configure(CardType.Skill, MadScienceRider.Wisdom);
                VisibleContent configured = VisibleContentProjection.ForCard(mad);
                Assert.Contains(VisibleValue.Int("Block", 8), configured.Values);
                Assert.Contains(VisibleValue.Int("WisdomCards", 3), configured.Values);
                Assert.DoesNotContain(configured.Values, value => value.Name == "Damage");
                var scythe = (TheScythe)ModelDb.Card<TheScythe>().MutableClone();
                typeof(TheScythe).GetField("_increasedDamage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(scythe, 10);
                Assert.Contains(VisibleValue.Number("Damage", 23), VisibleContentProjection.ForCard(scythe).Values);
                Assert.Contains(VisibleValue.Int("Increase", 5), VisibleContentProjection.ForCard(scythe).Values);
                var falling = (FallingStar)ModelDb.Card<FallingStar>().MutableClone();
                falling.SetToFreeThisTurn();
                Assert.Contains(VisibleValue.Int("StarCost", 0), VisibleContentProjection.ForCard(falling).Values);
                return;
            default: throw new InvalidOperationException(scenario);
        }
        foreach (object candidate in bindings)
        {
            Assert.Contains(projection.DescribeCandidate(candidate), projection.Metadata.DisplayOptions);
            Assert.Throws<ArgumentException>(() => projection.DescribeCandidate(new object()));
        }
        string saved = Serialize(projection.Metadata);
        player.Gold++;
        Assert.Equal(saved, Serialize(projection.Metadata));
    }

    private static VisibleDecisionSnapshot Pure(RunState run, Func<VisibleDecisionSnapshot> capture)
    {
        string before = Snapshot(run);
        VisibleDecisionSnapshot result = capture();
        Assert.Equal(Serialize(result.Metadata), Serialize(capture().Metadata));
        Assert.Equal(before, Snapshot(run));
        return result;
    }

    private static async Task VerifySelection(RunState run, Player player, CardModel[] cards, string scenario)
    {
        var selector = new SelectionProbe(); run.ConfigureCardSelectionSource(selector);
        var trace = new TraceProbe();
        int min = 1, max = 1;
        CardModel[] candidates = scenario == "CardAutomatic" ? cards.Take(1).ToArray() : cards;
        if (scenario == "CardBundle")
        {
            IReadOnlyList<IReadOnlyList<CardModel>> bundles = new IReadOnlyList<CardModel>[] { [cards[0], cards[1]], [cards[2]] };
            var request = new CardSelectionRequest(player, [cards[0], cards[2]], 1, 1, player.Character, Bundles: bundles);
            var snapshot = Pure(run, () => VisibleDecisionProjection.ForCards(request));
            Assert.Equal(2, snapshot.DescribeCandidate(cards[0]).Contents.Length);
            IReadOnlyList<CardModel> baseline = await CardSelectCmd.FromChooseABundleScreen(player, bundles, player.Character);
            using (CardSelectCmd.PushTraceObserver(trace))
            {
                IReadOnlyList<CardModel> observed = await CardSelectCmd.FromChooseABundleScreen(player, bundles, player.Character);
                Assert.Equal(baseline, observed);
            }
            Assert.Equal(2, trace.Selected!.Count);
        }
        else
        {
            var request = new CardSelectionRequest(player, candidates, min, max, player.Character);
            var snapshot = Pure(run, () => VisibleDecisionProjection.ForCards(request));
            Assert.Equal(candidates.Length, snapshot.Metadata.DisplayOptions.Length);
            Assert.Contains(VisibleValue.Int("MinCount", 1), snapshot.Metadata.ContextValues);
            IReadOnlyList<CardModel> baseline = await CardSelectCmd.SelectCardsAsync(run, player, candidates, min, max, player.Character);
            int calls = selector.Calls, automatic = selector.AutomaticCalls;
            using (CardSelectCmd.PushTraceObserver(trace))
            {
                IReadOnlyList<CardModel> observed = await CardSelectCmd.SelectCardsAsync(run, player, candidates, min, max, player.Character);
                Assert.Equal(baseline, observed);
            }
            Assert.Equal(scenario == "CardAutomatic" ? 0 : 1, selector.Calls - calls);
            Assert.Equal(scenario == "CardAutomatic" ? 1 : 0, selector.AutomaticCalls - automatic);
            Assert.Equal(scenario == "CardAutomatic", trace.Automatic);
            if (scenario == "CardManual")
            {
                var scoped = new SelectionProbe(last: true); var scopedTrace = new TraceProbe();
                using (CardSelectCmd.PushSelector(scoped))
                {
                    var scopedBaseline = await CardSelectCmd.SelectCardsAsync(run, player, candidates, min, max, player.Character);
                    int scopedCalls = scoped.Calls, scopedAutomatic = scoped.AutomaticCalls;
                    using (CardSelectCmd.PushTraceObserver(scopedTrace))
                    {
                        var selected = await CardSelectCmd.SelectCardsAsync(run, player, candidates, min, max, player.Character);
                        Assert.Equal(scopedBaseline, selected);
                        Assert.Same(cards[^1], Assert.Single(selected));
                    }
                    Assert.Equal(1, scoped.Calls - scopedCalls); Assert.Equal(0, scoped.AutomaticCalls - scopedAutomatic);
                }
                Assert.Equal(1, scopedTrace.Before); Assert.Equal(1, scopedTrace.After);
            }
        }
        Assert.Equal(1, trace.Before); Assert.Equal(1, trace.After);
        if (scenario == "CardAutomatic")
        {
            var emptyTrace = new TraceProbe();
            var emptyBaseline = await CardSelectCmd.SelectCardsAsync(run, player, [], 1, 1, player.Character);
            int calls = selector.Calls, automatic = selector.AutomaticCalls;
            using (CardSelectCmd.PushTraceObserver(emptyTrace))
                Assert.Equal(emptyBaseline, await CardSelectCmd.SelectCardsAsync(run, player, [], 1, 1, player.Character));
            Assert.Empty(emptyBaseline); Assert.Equal(calls, selector.Calls); Assert.Equal(1, selector.AutomaticCalls - automatic);
            Assert.Equal(1, emptyTrace.Before); Assert.Equal(1, emptyTrace.After); Assert.True(emptyTrace.Automatic);
        }
    }

    private sealed class SelectionProbe(bool last = false) : ICardSelectionDecisionSource, IAutomaticCardSelectionObserver
    {
        public int Calls { get; private set; }
        public int AutomaticCalls { get; private set; }
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<CardModel>>((last ? request.Candidates.Reverse() : request.Candidates).Take(request.MinCount).ToArray());
        }
        public void ObserveAutomaticSelection(CardSelectionRequest request, IReadOnlyList<CardModel> selected) => AutomaticCalls++;
    }
    private sealed class TraceProbe : ICardSelectionTraceObserver
    {
        public int Before { get; private set; }
        public int After { get; private set; }
        public bool Automatic { get; private set; }
        public IReadOnlyList<CardModel>? Selected { get; private set; }
        public void BeforeSelection(CardSelectionRequest request) { Before++; _ = VisibleDecisionProjection.ForCards(request); }
        public void AfterSelection(CardSelectionRequest request, IReadOnlyList<CardModel> selected, bool automatic)
        { After++; Automatic = automatic; Selected = selected; }
    }

    private static async Task<(RunState Run, Player Player, EventModel Event)> Setup(string eventType, CharacterModel? character = null)
    {
        var run = new RunState("visible-634-" + eventType, new Overgrowth());
        Player player = Player.CreateForNewRun(character ?? ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        player.Gold = 1000;
        player.Creature.SetMaxHpInternal(101);
        player.Creature.SetCurrentHpInternal(40);
        if (eventType is "TheFutureOfPotions" or "StoneOfAllTime" or "RanwidTheElder")
        {
            player.AddPotionInternal((PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone());
            player.AddPotionInternal((PotionModel)ModelDb.Potion<SwiftPotion>().MutableClone());
            player.AddPotionInternal((PotionModel)ModelDb.Potion<WeakPotion>().MutableClone());
        }
        if (eventType == "DenseVegetation") await RelicCmd.Obtain(ModelDb.Relic<RegalPillow>(), player);
        if (eventType == "RelicTrader")
        {
            await RelicCmd.Obtain(ModelDb.Relic<RegalPillow>(), player);
            await RelicCmd.Obtain(ModelDb.Relic<Anchor>(), player);
            await RelicCmd.Obtain(ModelDb.Relic<BagOfPreparation>(), player);
            await RelicCmd.Obtain(ModelDb.Relic<HappyFlower>(), player);
            await RelicCmd.Obtain(ModelDb.Relic<BloodVial>(), player);
        }
        Type type = ContentRegistry.AllTypes.Single(t => typeof(EventModel).IsAssignableFrom(t) && !t.IsAbstract && t.Name == eventType);
        var ev = (EventModel)ModelDb.Get(type).MutableClone();
        Assert.Empty(AllValues(ev.GetVisibleState().Metadata));
        Assert.Empty(ev.GetVisibleState().Metadata.DisplayOptions);
        var room = new EventRoom(() => ev);
        run.PushRoom(room);
        await room.Enter(run);
        return (run, player, ev);
    }

    private static string Snapshot(RunState run) => RunCloneGraphSnapshot.Capture(run);
    private static string Serialize(EventVisibleState state) => JsonSerializer.Serialize(state);
    private static IEnumerable<EventVisibleValue> AllValues(EventVisibleState state) =>
        state.ContextValues.Concat(state.DisplayOptions.SelectMany(option => option.Values));
    private static EventVisibleValue Value(EventVisibleState state, string key, string name) =>
        Assert.Single(Assert.Single(state.DisplayOptions.Where(option => option.OptionKey == key)).Values.Where(value => value.Name == name));
    private static int Integer(EventVisibleState state, string key, string name) => int.Parse(Value(state, key, name).Value, CultureInfo.InvariantCulture);
    private static void AssertValue(EventVisibleState state, string key, string name, string value, EventVisibleValueKind kind = EventVisibleValueKind.Integer) =>
        Assert.Equal(new EventVisibleValue(name, kind, value), Value(state, key, name));
}
