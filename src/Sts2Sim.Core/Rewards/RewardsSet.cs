using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rewards;

/// <summary>Rewards generated for a combat victory. 偏离 #78：不实现 Boss 专属遗物掉落——真实游戏
/// 击败 Boss 后走独立逻辑，不在这条通用的按房间类型生成奖励的路径里。</summary>
public sealed class RewardsSet
{
    public Player Player => Gold.Player;

    public GoldReward Gold { get; }

    public PotionReward? Potion { get; }

    public CardReward Card { get; }

    public RelicReward? Relic { get; }

    public IReadOnlyList<Reward> ExtraRewards { get; }

    /// <summary>逐字对应游戏 <c>RewardsSet.WithSkippingDisallowed</c>（#1094）：这个界面没有“离开”，每一项都必须领取。
    /// 自己构造奖励界面的驱动方在还有可领奖励时不得提供离开，也不得对其中的奖励直接调用
    /// <see cref="TakeableReward.Skip"/>。游戏里唯一的调用点是 <c>NeowsBones.AfterObtained</c>。</summary>
    public bool SkippingDisallowed { get; }

    private RewardsSet(
        GoldReward gold,
        PotionReward? potion,
        CardReward card,
        RelicReward? relic,
        IReadOnlyList<Reward> extraRewards,
        bool skippingDisallowed = false)
    {
        Gold = gold;
        Potion = potion;
        Card = card;
        Relic = relic;
        ExtraRewards = extraRewards;
        SkippingDisallowed = skippingDisallowed;
    }

    internal RewardsSet CloneForRun(Func<Reward, Reward> mapReward) =>
        new((GoldReward)mapReward(Gold),
            Potion is null ? null : (PotionReward)mapReward(Potion),
            (CardReward)mapReward(Card),
            Relic is null ? null : (RelicReward)mapReward(Relic),
            Array.AsReadOnly(ExtraRewards.Select(mapReward).ToArray()),
            SkippingDisallowed);

    public static RewardsSet GenerateFor(
        Player player,
        RoomType roomType,
        IRunState runState,
        IEnumerable<Reward>? pendingExtraRewards = null,
        int? fixedGoldAmount = null,
        EncounterDefinition? encounter = null,
        float goldProportion = 1f)
    {
        if (roomType is not (RoomType.Monster or RoomType.Elite or RoomType.Boss))
        {
            throw new ArgumentOutOfRangeException(nameof(roomType), roomType, "Only combat room types yield rewards.");
        }

        string rewardKey = runState is RunState concreteRun
            ? $"{concreteRun.SemanticLocationKey}/reward_kind={roomType}"
            : $"floor={runState.TotalFloor}/room_kind={runState.CurrentRoom?.RoomType.ToString() ?? "none"}" +
              $"/room_id={runState.CurrentRoom?.Id ?? 0}/reward_kind={roomType}";
        bool keyed = player.PlayerRng.UsesSemanticKeys;

        var replaced = new List<Reward>();
        bool isReplaced = runState.CurrentRoom is CombatRoom currentCombat && currentCombat.RoomType == roomType
            ? Hooks.Hook.TryReplaceDefaultRoomRewards(runState, player, replaced, currentCombat)
            : runState.RunModels.OfType<Models.Blitz.BlitzRunModel>().Any() &&
              Models.Blitz.BlitzRunModel.TryBuildDefaultRoomRewards(
                  player,
                  replaced,
                  roomType,
                  encounter?.IsWeak ?? false,
                  (runState as RunState)?.CurrentActIndex ?? 0);

        if (isReplaced)
        {
            return GenerateReplacedRewards(
                player,
                roomType,
                runState,
                rewardKey,
                keyed,
                replaced,
                pendingExtraRewards,
                fixedGoldAmount,
                encounter);
        }

        // #310: retain the existing non-null gold slot at proportion zero; its payout is zero.
        var gold = fixedGoldAmount is { } amount
            ? new GoldReward(amount, player)
            : new GoldReward(roomType, player, goldProportion, encounter?.MinGoldReward, encounter?.MaxGoldReward);

        Random.Rng potionOddsRng = player.PlayerRng.ForSemanticKey(
            PlayerRngType.Rewards,
            $"{rewardKey}/slot=potion_presence");

        PotionReward? potion = null;
        if (player.Odds.PotionReward.Roll(roomType, potionOddsRng))
        {
            potion = new PotionReward(player);

        }

        // Upstream constructs the reward list (including the potion roll) before populating it.
        gold.Populate(
            runState,
            player.PlayerRng.ForSemanticKey(PlayerRngType.Rewards, $"{rewardKey}/slot=gold"));
        potion?.Populate(
            runState,
            player.PlayerRng.ForSemanticKey(PlayerRngType.Rewards, $"{rewardKey}/slot=potion"));

        CardRarityOddsType oddsType = roomType switch
        {
            RoomType.Elite => CardRarityOddsType.EliteEncounter,
            RoomType.Boss => CardRarityOddsType.BossEncounter,
            _ => CardRarityOddsType.RegularEncounter,
        };
        CardReward card = keyed
            ? new CardReward(
                player,
                new CardCreationOptions(
                        [player.Character.CardPool],
                        CardCreationSource.Encounter,
                        oddsType)
                    .WithRngOverride(player.PlayerRng.ForSemanticKey(
                        PlayerRngType.Rewards,
                        $"{rewardKey}/slot=card")))
            : new CardReward(player, oddsType);
        card.Populate(runState);

        RelicReward? relic = null;
        if (roomType == RoomType.Elite)
        {
            relic = new RelicReward(player);
            relic.Populate(
                runState,
                player.PlayerRng.ForSemanticKey(PlayerRngType.Rewards, $"{rewardKey}/slot=relic"));
        }

        var extraRewards = pendingExtraRewards?.ToList() ?? new List<Reward>();
        Hooks.Hook.ModifyRewards(runState, player, extraRewards, roomType);

        return new RewardsSet(gold, potion, card, relic, extraRewards);
    }

    private static RewardsSet GenerateReplacedRewards(
        Player player,
        RoomType roomType,
        IRunState runState,
        string rewardKey,
        bool keyed,
        List<Reward> replaced,
        IEnumerable<Reward>? pendingExtraRewards,
        int? fixedGoldAmount,
        EncounterDefinition? encounter)
    {
        GoldReward? gold = null;
        PotionReward? potion = null;
        CardReward? card = null;
        RelicReward? relic = null;
        var extraRewards = new List<Reward>();
        int potionIndex = 0;
        int relicIndex = 0;

        foreach (Reward item in replaced)
        {
            switch (item)
            {
                case GoldReward g:
                    g.Populate(
                        runState,
                        player.PlayerRng.ForSemanticKey(PlayerRngType.Rewards, $"{rewardKey}/slot=gold"));
                    if (gold is null) gold = g;
                    else extraRewards.Add(g);
                    break;

                case CardReward c:
                    if (keyed)
                    {
                        c.ConfigureRngOverrideIfMissing(
                            player.PlayerRng.ForSemanticKey(PlayerRngType.Rewards, $"{rewardKey}/slot=card"));
                    }
                    c.Populate(runState);
                    if (card is null) card = c;
                    else extraRewards.Add(c);
                    break;

                case PotionReward p:
                    string potionSlot = potionIndex == 0 ? "slot=potion" : $"slot=potion_{potionIndex + 1}";
                    potionIndex++;
                    p.Populate(
                        runState,
                        player.PlayerRng.ForSemanticKey(PlayerRngType.Rewards, $"{rewardKey}/{potionSlot}"));
                    if (potion is null) potion = p;
                    else extraRewards.Add(p);
                    break;

                case RelicReward r:
                    string relicSlot = relicIndex == 0 ? "slot=relic" : $"slot=relic_{relicIndex + 1}";
                    relicIndex++;
                    r.Populate(
                        runState,
                        player.PlayerRng.ForSemanticKey(PlayerRngType.Rewards, $"{rewardKey}/{relicSlot}"));
                    if (relic is null) relic = r;
                    else extraRewards.Add(r);
                    break;

                default:
                    item.Populate(runState);
                    extraRewards.Add(item);
                    break;
            }
        }

        if (gold is null)
        {
            gold = new GoldReward(fixedGoldAmount ?? 0, player);
            if (fixedGoldAmount is null)
                gold.Skip().GetAwaiter().GetResult();
        }
        card ??= CardReward.CreateResolvedEmpty(player);

        if (pendingExtraRewards is not null)
        {
            extraRewards.AddRange(pendingExtraRewards);
        }

        Hooks.Hook.ModifyRewards(runState, player, extraRewards, roomType);
        return new RewardsSet(gold, potion, card, relic, extraRewards);
    }

    public static RewardsSet CreateCustom(
        Player player,
        GoldReward? gold = null,
        CardReward? card = null,
        PotionReward? potion = null,
        RelicReward? relic = null,
        IReadOnlyList<Reward>? extraRewards = null,
        bool skippingDisallowed = false)
    {
        ArgumentNullException.ThrowIfNull(player);

        Reward[] extras = extraRewards?.ToArray() ?? Array.Empty<Reward>();
        if (extras.Any(reward => reward is null))
        {
            throw new ArgumentException("Extra rewards cannot contain null.", nameof(extraRewards));
        }

        return new RewardsSet(
            gold ?? new GoldReward(amount: 0, player),
            potion,
            card ?? CardReward.CreateResolvedEmpty(player),
            relic,
            Array.AsReadOnly(extras),
            skippingDisallowed);
    }
}
