using System.Reflection;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs;

internal enum ImaginationFieldClass { Visible, HiddenPrerolled, NotRandom }

internal static class ImaginationHiddenStateRegistry
{
    private static readonly IReadOnlyDictionary<string, (ImaginationFieldClass Class, string Reason)> Entries = Build();

    public static IReadOnlyDictionary<string, (ImaginationFieldClass Class, string Reason)> Fields => Entries;

    internal static void ValidateCoverage()
    {
        Assembly assembly = typeof(EventModel).Assembly;
        var relevant = assembly.GetTypes().Where(type =>
            type.IsClass && !type.ContainsGenericParameters &&
            (typeof(EventModel).IsAssignableFrom(type) ||
             typeof(Reward).IsAssignableFrom(type) ||
             typeof(AbstractRoom).IsAssignableFrom(type) ||
             typeof(RestSiteDecision).IsAssignableFrom(type) ||
             typeof(MerchantEntry).IsAssignableFrom(type) ||
             type == typeof(RewardsSet) || type == typeof(CardRewardAlternative) || type == typeof(TreasureRoomResolution) ||
             type == typeof(MerchantInventory)));

        var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (Type type in relevant)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                string key = Key(type, field.Name);
                actual.Add(key);
                if (!Entries.ContainsKey(key))
                    throw new InvalidOperationException($"Unclassified imagination field: {key}");
            }
        }

        foreach (string key in Entries.Keys)
            if (!actual.Contains(key))
                throw new InvalidOperationException($"Stale imagination field registration: {key}");
    }

    private static string Key(Type type, string name) => $"{type.FullName}.{name}";

    private static IReadOnlyDictionary<string, (ImaginationFieldClass Class, string Reason)> Build()
    {
        var fields = new Dictionary<string, (ImaginationFieldClass, string)>(StringComparer.Ordinal);
        Assembly assembly = typeof(EventModel).Assembly;
        void Add(string typeName, ImaginationFieldClass fieldClass, string reason, string names)
        {
            Type type = assembly.GetType(typeName) ??
                throw new InvalidOperationException($"Missing registered imagination type: {typeName}");
            foreach (string name in names.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                if (!fields.TryAdd(Key(type, name), (fieldClass, reason)))
                    throw new InvalidOperationException($"Duplicate imagination field registration: {typeName}.{name}");
        }
        void Event(string name, ImaginationFieldClass fieldClass, string reason, string names) =>
            Add($"Sts2Sim.Core.Models.Events.{name}", fieldClass, reason, names);
        void RewardField(string name, ImaginationFieldClass fieldClass, string reason, string names) =>
            Add($"Sts2Sim.Core.Rewards.{name}", fieldClass, reason, names);
        void Room(string name, ImaginationFieldClass fieldClass, string reason, string names) =>
            Add($"Sts2Sim.Core.Rooms.{name}", fieldClass, reason, names);
        void Merchant(string name, ImaginationFieldClass fieldClass, string reason, string names) =>
            Add($"Sts2Sim.Core.Entities.Merchant.{name}", fieldClass, reason, names);

        const ImaginationFieldClass V = ImaginationFieldClass.Visible;
        const ImaginationFieldClass N = ImaginationFieldClass.NotRandom;
        const ImaginationFieldClass H = ImaginationFieldClass.HiddenPrerolled;
        const string visible = "Already observable from the current page, reward, stock, or prior choice.";
        const string history = "Choice/page progress or derived value fixed by visible history.";
        const string runtime = "World-owned bindings are rebuilt, locks/tasks are not copied, and active callbacks are rejected; this table is not proof of a successful clone.";
        const string reward = "Preserve only actually displayed reward/stock fields; pre-offer hidden values require mode-specific rebuilding. Classification alone does not establish display state.";

        Add(typeof(EventModel).FullName!, N, runtime,
            "_stateLock _owner _runState _activeChoiceTask _activeChoiceOption _pendingForcedCombatFactory _pendingForcedCombatBatchFactory _pendingForcedCombatSlottedBatchFactory");
        Add(typeof(EventModel).FullName!, N, "Event-local future RNG is replaced with a fresh root-derived stream.", "_rng");
        Add(typeof(EventModel).FullName!, V, history,
            "_activeChoicePageVersion _pageVersion _hasBegun _isAwaitingForcedCombat _pendingRewardOffers _suspendedRewardOffers <CurrentOptions>k__BackingField <IsFinished>k__BackingField");

        Event("AbyssalBaths", V, history, "_lingerCount _damage");
        Event("BattlewornDummy", V, history, "_selectedTier");
        Event("ColossalFlower", V, history, "_dig");
        Event("CrystalSphere", V, visible, "<UncoverFutureCost>k__BackingField");
        Event("CrystalSphere", H, "Game contains concealed pre-placed board occupants; reroll them while retaining revealed cells and items.", "<Game>k__BackingField");
        Event("DenseVegetation", V, "Displayed event gold DynamicVar.", "_gold");
        Event("EndlessConveyor", V, history, "_lastDishId _currentDishId _numOfGrabs");
        Event("FakeMerchant", N, runtime, "_interactionGate");
        Event("FakeMerchant", V, visible, "_combatRewards <Inventory>k__BackingField <IsInventoryOpen>k__BackingField <StartedFight>k__BackingField");
        Event("JungleMazeAdventure", V, "Displayed event gold DynamicVars.", "_soloGold _joinForcesGold");
        Event("LostWisp", V, "Displayed event gold DynamicVar.", "_gold");
        Event("LuminousChoir", V, "Displayed tribute cost and enabled option.", "_cost");
        Event("PunchOff", V, reward, "_combatRewards");
        Event("RanwidTheElder", V, "Offered potion and relic are exposed by the current event page.", "<PotionOffered>k__BackingField <RelicOffered>k__BackingField");
        Event("RelicTrader", V, "Current trade offer exposes both relic columns.", "<OwnedRelics>k__BackingField <NewRelics>k__BackingField");
        Event("SlipperyBridge", V, "Current page exposes RandomCardToLose, skipped history, and HP cost.", "_skipped _randomCard _holdOns");
        Event("SpiralingWhirlpool", V, "Heal is derived from visible maximum HP.", "_heal");
        Event("StoneOfAllTime", V, "Selected potion title and HoverTip are shown before LIFT.", "_potion");
        Event("SunkenStatue", V, "Displayed event gold DynamicVar.", "_gold");
        Event("SunkenTreasury", V, "Displayed event gold DynamicVars.", "_smallChestGold _largeChestGold");
        Event("TabletOfTruth", V, history, "_decipherCount _currentCost");
        Event("TheFutureOfPotions", V, "Each rolled card type is shown in its POTION option description.", "_cardTypes");
        Event("TheLanternKey", V, reward, "_combatRewards");
        Event("ThisOrThat", V, "Displayed event gold DynamicVar.", "_gold");
        Event("TinkerTime", V, history, "_chosenType");
        Event("WelcomeToWongos", V, "Featured relic is shown on the current offer.", "_featuredItem");
        Event("WhisperingHollow", V, "Displayed event cost DynamicVar.", "_cost");

        RewardField("Reward", V, runtime, "<Player>k__BackingField <IsResolved>k__BackingField");
        RewardField("TakeableReward", N, runtime, "_takeLock _takeTask");
        RewardField("CardReward", N, runtime, "_resolutionLock _resolutionTask _resolutionIdentity");
        RewardField("CardReward", V, reward,
            "_oddsType _explicitOptions _creationOptions _rerollOptions _optionCount _isPopulated _hasBeenRerolled _alternatives _alternativeRelics <Options>k__BackingField <SelectedOption>k__BackingField <CanReroll>k__BackingField");
        RewardField("CardRewardAlternative", N, runtime, "_onSelect _isAvailable");
        RewardField("CardRewardAlternative", V, visible, "<OptionId>k__BackingField <CompletesReward>k__BackingField");
        RewardField("GoldReward", V, reward,
            "_representsOmittedReward _min _max <Amount>k__BackingField");
        RewardField("PotionReward", V, reward, "<Potion>k__BackingField <IsOptionalMerchantChoice>k__BackingField");
        RewardField("RelicReward", V, reward, "_predeterminedRelic _rarity <Relic>k__BackingField");
        RewardField("SpecialCardReward", V, reward, "<Card>k__BackingField");
        RewardField("RewardsSet", V, reward,
            "<Gold>k__BackingField <Potion>k__BackingField <Card>k__BackingField <Relic>k__BackingField <ExtraRewards>k__BackingField");

        Merchant("MerchantInventory", V, reward,
            "_player _cards _relics _potions <CardRemoval>k__BackingField");
        Merchant("MerchantEntry", V, reward, "_basePrice _player <Purchased>k__BackingField");
        Merchant("MerchantCardEntry", V, reward, "<Card>k__BackingField");
        Merchant("MerchantPotionEntry", V, reward, "<Potion>k__BackingField");
        Merchant("MerchantRelicEntry", V, reward, "<Relic>k__BackingField");

        Room("AbstractRoom", V, history, "_hasEntered <Id>k__BackingField");
        Room("CombatRoom", N, runtime,
            "_monsterFactory _monsterBatchFactory _encounterFactory _slottedMonsterBatchFactory _lifecycleLock _insideLifecycle _outcomeTask _exitTask _completionTask _observer _cardSelectionSource _beforeSetupDiagnostic");
        Room("CombatRoom", H, "Combat state prepared before FIGHT is rebuilt with imagination RNG.", "_preparedCombatState");
        Room("CombatRoom", V, history, "_outcomeCompleted _exitCompleted _completionCompleted");
        Room("CombatRoom", V, reward,
            "_pendingExtraRewards _unpopulatedExtraRewards <FixedGoldAmount>k__BackingField <Encounter>k__BackingField <EncounterName>k__BackingField <RoomType>k__BackingField <ModelId>k__BackingField <Engine>k__BackingField <Won>k__BackingField <GoldProportion>k__BackingField <GeneratedRewards>k__BackingField");
        Room("EventRoom", N, runtime, "_eventFactory");
        Room("EventRoom", H, "Hidden prepared combat is rebuilt before an event choice.", "_preparedCombatRoom");
        Room("EventRoom", V, history, "<Event>k__BackingField");
        Room("MerchantRoom", N, runtime, "_purchaseGate _rewardLock");
        Room("MerchantRoom", V, reward,
            "_inventories _inventoryRunState _displayPlayer _isActive _rewardOffers <IsInventoryOpen>k__BackingField");
        Room("RestSiteRoom", N, runtime, "_rewardLock");
        Room("RestSiteRoom", V, history, "_pendingRewardOffers _remainingDecisions");
        Room("TreasureRoom", V, reward, "_goldAmount _resolutions");
        Room("TreasureRoomResolution", V, reward,
            "<Player>k__BackingField <GoldGained>k__BackingField <RelicGained>k__BackingField");

        Add(typeof(RestSiteDecision).FullName! + "+Smith", V, "Card target was chosen by the player.", "<Card>k__BackingField");
        return fields;
    }
}
