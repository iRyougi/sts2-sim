using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Blitz;

public sealed class BlitzRunModel : RunModel
{
    public static IReadOnlyList<(EnchantmentModel Enchantment, int Weight)> GetShopEnchantmentOptions() =>
    [
        (ModelDb.GetById<EnchantmentModel>(ModelDb.GetId<Sharp>()), 2),
        (ModelDb.GetById<EnchantmentModel>(ModelDb.GetId<Nimble>()), 3),
        (ModelDb.GetById<EnchantmentModel>(ModelDb.GetId<Adroit>()), 3),
        (ModelDb.GetById<EnchantmentModel>(ModelDb.GetId<Steady>()), 1),
        (ModelDb.GetById<EnchantmentModel>(ModelDb.GetId<Swift>()), 2),
        (ModelDb.GetById<EnchantmentModel>(ModelDb.GetId<Vigorous>()), 5),
    ];

    public override bool TryModifyCardRewardOptionsLate(
        Player player,
        List<CardModel> options,
        CardCreationOptions creationOptions)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(creationOptions);

        IRunState runState = player.RunState;
        if (runState is not RunState concreteRun || concreteRun.CurrentActIndex == 0)
        {
            return false;
        }

        if (!creationOptions.Flags.HasFlag(CardCreationFlags.IsFromCombat))
        {
            return false;
        }

        if (runState.CurrentRoom is { RoomType: RoomType.Boss })
        {
            return false;
        }

        if (options.Count == 0)
        {
            return false;
        }

        Rng rng = creationOptions.RngOverride ?? player.PlayerRng.Rewards;
        if (concreteRun.CurrentActIndex == 1)
        {
            CardModel? chosen = rng.NextItem(options);
            if (chosen is not null)
            {
                UpgradeOptionInPlace(options, options.IndexOf(chosen), player);
            }
        }
        else if (concreteRun.CurrentActIndex == 2)
        {
            options.UnstableShuffle(rng);
            int upgradeCount = Math.Min(2, options.Count);
            for (int i = 0; i < upgradeCount; i++)
            {
                UpgradeOptionInPlace(options, i, player);
            }
        }

        return true;
    }

    public override bool TryReplaceDefaultRoomRewards(Player player, List<Reward> rewards, AbstractRoom room)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(room);

        int currentActIndex = RunState is RunState concreteRun ? concreteRun.CurrentActIndex : 0;
        bool isWeakEncounter = room is CombatRoom combatRoom && (combatRoom.Encounter?.IsWeak ?? false);
        return TryBuildDefaultRoomRewards(player, rewards, room.RoomType, isWeakEncounter, currentActIndex);
    }

    internal static bool TryBuildDefaultRoomRewards(
        Player player,
        List<Reward> rewards,
        RoomType roomType,
        bool isWeakEncounter,
        int currentActIndex)
    {
        switch (roomType)
        {
            case RoomType.Monster:
                rewards.Add(new CardReward(player, GetCardRewardOptions(player, CardRarity.Common), 3));
                if (!isWeakEncounter)
                {
                    rewards.Add(new PotionReward(player));
                }
                return true;

            case RoomType.Elite:
                rewards.Add(new CardReward(player, GetCardRewardOptions(player, CardRarity.Uncommon), 3));
                rewards.Add(new RelicReward(player));
                rewards.Add(new RelicReward(player));
                return true;

            case RoomType.Boss when currentActIndex < 2:
                rewards.Add(new CardReward(player, GetCardRewardOptions(player, CardRarity.Rare), 3));
                rewards.Add(new PotionReward(player));
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Blitz 的战斗选牌奖励使用固定稀有度且不做随机升级检定，通过 IsFromCombat 在第 2/3 幕后期强化。
    /// </summary>
    public static CardCreationOptions GetCardRewardOptions(Player player, CardRarity rarity) =>
        CardCreationOptions.ForNonCombatWithUniformOdds(
                new CardPoolModel[] { player.Character.CardPool },
                card => card.Rarity == rarity)
            .WithFlags(CardCreationFlags.NoUpgradeRoll | CardCreationFlags.IsFromCombat);

    private static void UpgradeOptionInPlace(List<CardModel> options, int index, Player player)
    {
        if (index < 0 || index >= options.Count)
        {
            return;
        }

        CardModel card = options[index];
        if (!card.IsUpgradable)
        {
            return;
        }

        var clone = (CardModel)card.MutableClone();
        clone.AssignOwner(player);
        CardCmd.Upgrade(clone);
        options[index] = clone;
    }
}
