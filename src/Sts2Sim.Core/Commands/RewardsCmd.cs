using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Commands;

/// <summary>Offers custom rewards through the current event, rest-site, or merchant decision loop.
/// Uses the existing pending-offer adaptation in deviation #169.</summary>
public static class RewardsCmd
{
    public static Task OfferCustom(Player player, List<Reward> rewards)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(rewards);
        if (player.Creature.IsDead) return Task.CompletedTask;

        var offered = rewards.ToList();
        if (offered.Any(reward => reward is null))
        {
            throw new ArgumentException("Custom rewards cannot contain null.", nameof(rewards));
        }
        if (offered.Any(reward => !ReferenceEquals(reward.Player, player)))
            throw new ArgumentException("Custom rewards must belong to the receiving player.", nameof(rewards));
        if (player.RunState.CurrentRoom is MerchantRoom merchant && !merchant.CanOfferRewards(player))
            throw new InvalidOperationException("Custom merchant rewards require the active player's merchant.");
        for (int index = 0; index < offered.Count; index++)
        {
            PopulateWithSemanticSlot(offered[index], player, "initial", index);
        }
        var original = offered.ToHashSet();
        // 偏离 #109：现有奖励 hook 用 RoomType 表示来源；Unassigned 对应上游自定义奖励的 null room。
        Hook.ModifyRewards(player.RunState, player, offered, RoomType.Unassigned);
        Reward[] added = offered.Except(original).ToArray();
        for (int index = 0; index < added.Length; index++)
        {
            PopulateWithSemanticSlot(added[index], player, "hook_added", index);
        }
        if (offered.Count == 0) return Task.CompletedTask;
        if (offered.Any(reward => !ReferenceEquals(reward.Player, player)))
            throw new InvalidOperationException("Reward modifiers returned a reward owned by another player.");
        if (player.RunState.CurrentRoom is MerchantRoom)
            foreach (PotionReward potion in offered.OfType<PotionReward>()) potion.IsOptionalMerchantChoice = true;

        RewardsSet offer = RewardsSet.CreateCustom(player, extraRewards: offered);
        bool accepted = player.RunState.CurrentRoom switch
        {
            EventRoom room => room.Event.TryOfferRewardsFromCurrentEvent(player.RunState, offer),
            RestSiteRoom room => room.TryOfferRewardsFromCurrentRestSite(player.RunState, offer),
            MerchantRoom room => room.TryOfferRewardsFromCurrentMerchant(player.RunState, offer),
            _ => false,
        };
        if (!accepted)
        {
            throw new InvalidOperationException("Custom rewards require a current event, rest site, or merchant.");
        }
        return Task.CompletedTask;
    }

    private static void PopulateWithSemanticSlot(
        Reward reward,
        Player player,
        string phase,
        int slot)
    {
        string semanticKey = $"{player.CurrentSemanticLocationKey}/custom_reward" +
                             $"/phase={phase}/slot={slot}/type={reward.GetType().Name}";
        using IDisposable runRngScope = player.RunState.Rng.BeginSemanticScope(semanticKey);
        using IDisposable playerRngScope = player.PlayerRng.BeginSemanticScope(semanticKey);
        reward.Populate(player.RunState);
    }
}
