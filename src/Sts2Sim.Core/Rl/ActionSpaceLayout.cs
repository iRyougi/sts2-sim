using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// Positional layout for the single flat action space.
/// </summary>
public static class ActionSpaceLayout
{
    public const int MaxHand = CardPile.MaxCardsInHand;

    public const int MaxEnemies = 5;

    public const int MaxMapChoices = 7;

    // Retain the original four wire indices; additional reward choices live at the tail.
    public const int MaxRewardChoices = 4;
    public const int MaxRewardOverflowChoices = 1;
    public const int MaxTotalRewardChoices = MaxRewardChoices + MaxRewardOverflowChoices;

    public const int MaxShopCardChoices = 7;

    public const int MaxShopRelicChoices = 3;

    public const int MaxShopPotionChoices = 3;

    /// <summary>
    /// Deviation #188: Gym requires a static action space, but the deck backing removal/smith/card
    /// choices is unbounded — <c>BingBong</c> duplicates a card into the deck every time one is
    /// added, and real runs can carry thousands of cards. Rather than cap raw deck instances,
    /// candidates are collapsed into decision-equivalence classes (same card type, upgrade level,
    /// and enchantment, including magnitude — see <see cref="CardEquivalenceKey"/>): a player
    /// deciding "remove a Strike" doesn't care which physical copy leaves. An invariant-probe
    /// sweep measured a maximum of 87 raw deck instances across 150 runs, and, across a 40-run
    /// sample split 10-with-<c>BingBong</c>/30-without, a maximum of 56 equivalence classes —
    /// BingBong roughly doubles instance counts (median 73 vs 47) but barely moves the class count
    /// (median 41 vs 39). 256 keeps roughly 4.5x headroom over the observed class maximum. This
    /// constant feeds <see cref="TotalActions"/> three times over — it independently backs
    /// <see cref="MaxShopChoices"/>, <see cref="MaxRestSiteChoices"/>, and
    /// <see cref="MaxCardSelectionChoices"/> — so raising it again triples its cost to the action
    /// space; weigh that before bumping further. Encoders must still throw on overflow rather than
    /// truncate candidates and hide a legal choice.
    /// </summary>
    public const int MaxDeckBackedChoices = 256;

    public const int MaxShopChoices =
        MaxShopCardChoices + MaxShopRelicChoices + MaxShopPotionChoices + MaxDeckBackedChoices + 1;

    public const int MaxRestSiteChoices = 1 + MaxDeckBackedChoices + 1;

    public const int MaxEventChoices = 3;

    public const int MaxCustomEventChoices = 128;

    public const int MaxCardSelectionChoices = MaxDeckBackedChoices;

    public const int PlayCardBase = 0;

    public const int EndTurnIndex = MaxHand * MaxEnemies;

    public const int MapPointBase = EndTurnIndex + 1;

    public const int RewardBase = MapPointBase + MaxMapChoices;

    public const int ShopBase = RewardBase + MaxRewardChoices;

    public const int RestSiteBase = ShopBase + MaxShopChoices;

    public const int EventBase = RestSiteBase + MaxRestSiteChoices;

    public const int CardSelectionBase = EventBase + MaxEventChoices;

    // Historical compatibility note: before #188, 0..208 were the original actions and
    // 209..272 were card choices. Raising MaxDeckBackedChoices shifted the later ranges;
    // consumers must use the computed bases below rather than those historical literals.
    public const int MaxPlayerTargetChoices = 64;

    public const int PlayerTargetBase = CardSelectionBase + MaxCardSelectionChoices;

    public const int CardSelectionConfirmIndex = PlayerTargetBase + MaxPlayerTargetChoices;

    public const int CardSelectionCancelIndex = CardSelectionConfirmIndex + 1;

    /// <summary>
    /// Cook retains its dedicated slot immediately before custom-event choices. Its numeric index
    /// moved when #188 widened deck-backed ranges, so callers must use this constant.
    /// </summary>
    public const int CookRestSiteIndex = CardSelectionCancelIndex + 1;

    /// <summary>
    /// Custom-event slots follow Cook. Together they end at the historical 1044-action milestone;
    /// the generic rest-site overflow range is appended after them.
    /// </summary>
    public const int CustomEventBase = CookRestSiteIndex + 1;

    /// <summary>
    /// Generic headroom for additional rest-site content, independent of the 256 Smith classes.
    /// Appended after the historical 1044-action layout to retain those wire indices (including
    /// Cook and custom events). Further content needs no type-specific slot; exceeding the 32
    /// extra choices throws. This range ends at 1076; the reward overflow follows it.
    /// </summary>
    public const int MaxRestSiteOverflowChoices = 32;

    public const int RestSiteOverflowBase = CustomEventBase + MaxCustomEventChoices;

    /// <summary>
    /// The real width of the flat action space. Plan 08b-6 briefly carried a second
    /// <c>ExpandedTotalActions</c> constant alongside a frozen <c>TotalActions</c>; the encoders
    /// moved to the new one while <see cref="ActionSpaceLayoutTests"/>' cross-language snapshot kept
    /// guarding the old one, so the Python <c>_ACTION_COUNT</c> drifted to 468 with the guard still
    /// green. There is deliberately only one total now.
    /// </summary>
    public const int RewardOverflowBase = RestSiteOverflowBase + MaxRestSiteOverflowChoices;
    public const int TotalActions = RewardOverflowBase + MaxRewardOverflowChoices;

    public static int PlayCardIndex(int handIndex, int enemyIndex) =>
        PlayCardBase + (handIndex * MaxEnemies) + enemyIndex;

    public static int MapPointIndex(int position) => MapPointBase + position;

    public static int RewardIndex(int position) => position < MaxRewardChoices
        ? Index(RewardBase, MaxRewardChoices, position)
        : Index(RewardOverflowBase, MaxRewardOverflowChoices, position - MaxRewardChoices);

    public static int ShopIndex(int position) => Index(ShopBase, MaxShopChoices, position);

    public static int RestSiteIndex(int position) => Index(RestSiteBase, MaxRestSiteChoices, position);

    public static int PackedRestSiteIndex(int position) => position < MaxRestSiteChoices
        ? RestSiteIndex(position)
        : Index(RestSiteOverflowBase, MaxRestSiteOverflowChoices, position - MaxRestSiteChoices);

    public static int EventIndex(int position) => Index(EventBase, MaxEventChoices, position);

    public static int CustomEventIndex(int position) => Index(CustomEventBase, MaxCustomEventChoices, position);

    public static int CardSelectionIndex(int position) =>
        Index(CardSelectionBase, MaxCardSelectionChoices, position);

    public static int PlayerTargetIndex(int position) =>
        Index(PlayerTargetBase, MaxPlayerTargetChoices, position);

    public static bool TryDecodePlayCard(int actionIndex, out int handIndex, out int enemyIndex)
    {
        if (actionIndex < PlayCardBase || actionIndex >= EndTurnIndex)
        {
            handIndex = 0;
            enemyIndex = 0;
            return false;
        }

        int offset = actionIndex - PlayCardBase;
        handIndex = offset / MaxEnemies;
        enemyIndex = offset % MaxEnemies;
        return true;
    }

    public static bool TryDecodeMapPoint(int actionIndex, out int position)
    {
        return TryDecodePosition(actionIndex, MapPointBase, MaxMapChoices, out position);
    }

    public static bool TryDecodeReward(int actionIndex, out int position)
    {
        if (TryDecodePosition(actionIndex, RewardBase, MaxRewardChoices, out position)) return true;
        if (!TryDecodePosition(actionIndex, RewardOverflowBase, MaxRewardOverflowChoices, out position)) return false;
        position += MaxRewardChoices;
        return true;
    }

    public static bool TryDecodeShop(int actionIndex, out int position) =>
        TryDecodePosition(actionIndex, ShopBase, MaxShopChoices, out position);

    public static bool TryDecodeRestSite(int actionIndex, out int position) =>
        TryDecodePosition(actionIndex, RestSiteBase, MaxRestSiteChoices, out position);

    public static bool TryDecodeCookRestSite(int actionIndex) => actionIndex == CookRestSiteIndex;

    public static bool TryDecodeEvent(int actionIndex, out int position) =>
        TryDecodePosition(actionIndex, EventBase, MaxEventChoices, out position);

    public static bool TryDecodeCustomEvent(int actionIndex, out int position) =>
        TryDecodePosition(actionIndex, CustomEventBase, MaxCustomEventChoices, out position);

    public static bool TryDecodeCardSelection(int actionIndex, out int position) =>
        TryDecodePosition(
            actionIndex,
            CardSelectionBase,
            MaxCardSelectionChoices,
            out position);

    public static bool TryDecodePlayerTarget(int actionIndex, out int position) =>
        TryDecodePosition(actionIndex, PlayerTargetBase, MaxPlayerTargetChoices, out position);

    private static int Index(int baseIndex, int count, int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        if (position >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return baseIndex + position;
    }

    private static bool TryDecodePosition(int actionIndex, int baseIndex, int count, out int position)
    {
        if (actionIndex < baseIndex || actionIndex >= baseIndex + count)
        {
            position = 0;
            return false;
        }

        position = actionIndex - baseIndex;
        return true;
    }
}
