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

    private RewardsSet(
        GoldReward gold,
        PotionReward? potion,
        CardReward card,
        RelicReward? relic,
        IReadOnlyList<Reward> extraRewards)
    {
        Gold = gold;
        Potion = potion;
        Card = card;
        Relic = relic;
        ExtraRewards = extraRewards;
    }

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

        // #310: retain the existing non-null gold slot at proportion zero; its payout is zero.
        var gold = fixedGoldAmount is { } amount
            ? new GoldReward(amount, player)
            : new GoldReward(roomType, player, goldProportion, encounter?.MinGoldReward, encounter?.MaxGoldReward);


        string rewardKey = runState is RunState concreteRun
            ? $"{concreteRun.SemanticLocationKey}/reward_kind={roomType}"
            : $"floor={runState.TotalFloor}/room_kind={runState.CurrentRoom?.RoomType.ToString() ?? "none"}" +
              $"/room_id={runState.CurrentRoom?.Id ?? 0}/reward_kind={roomType}";
        bool keyed = player.PlayerRng.UsesSemanticKeys;
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

    public static RewardsSet CreateCustom(
        Player player,
        GoldReward? gold = null,
        CardReward? card = null,
        PotionReward? potion = null,
        RelicReward? relic = null,
        IReadOnlyList<Reward>? extraRewards = null)
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
            Array.AsReadOnly(extras));
    }
}
