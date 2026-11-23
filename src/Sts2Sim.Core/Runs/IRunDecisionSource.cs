using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// Supplies map, combat, reward, shop, rest-site, and event choices when requested by <see cref="RunDriver"/>.
/// 偏离 #79（已解决，2026-08-31，Plan08a）：Plan05 当时只在 C# 核心层新增奖励/商店/事件决策，
/// 未扩展 RL 协议与 Python 动作空间；Plan08a 已把这三类决策及 Rest Site 完整接入统一 RL 链路。
/// 见 <c>docs/superpowers/archive/plans/2026-08-30-plan-08a-rl-action-space-completion.md</c> 与
/// <c>docs/superpowers/archive/reviews/2026-08-31-plan-08a-rl-action-space-completion-report.md</c>。
/// </summary>
public interface IRunDecisionSource : ICardSelectionDecisionSource
{
    Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options);

    Task<CombatDecision> ChooseCombatActionAsync(CombatState state);

    Task<IReadOnlyList<CardModel>> ICardSelectionDecisionSource.ChooseCardsAsync(
        CardSelectionRequest request) =>
        RunEngineCardSelectionDecisionSource.Instance.ChooseCardsAsync(request);

    Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        Task.FromResult(RewardDecisionClassifier.ChooseDefault(rewards));

    Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        Task.FromResult<ShopDecision>(new ShopDecision.Leave());

    Task<CustomEventDecision> ChooseCustomEventActionAsync(EventModel @event) =>
        Task.FromResult(CustomEventDecisionPolicy.ChooseDefault(@event));

    /// <summary>
    /// Chooses from the immutable candidate snapshot emitted by the current <c>RestSiteRoom</c>.
    /// Implementations must return one of these candidates; <see cref="RunDriver"/> verifies it.
    /// </summary>
    Task<RestSiteDecision> ChooseRestSiteActionAsync(
        Player player,
        IReadOnlyList<RestSiteDecision> candidates) =>
        Task.FromResult(RestSiteDecisionPolicy.ChooseDefault(candidates));

    Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
        Task.FromResult(EventOptionDecisionPolicy.ChooseDefault(options));

    /// <summary>
    /// Chest relic choice in a treasure room (native TreasureRoomRelicSynchronizer.PickRelicLocally / SkipRelicLocally), asked
    /// after the chest's gold: the index of the relic to take, or null to skip it (the gold is kept, the relic is not
    /// obtained). Default: take the first relic, without a decision.
    /// </summary>
    Task<int?> ChooseTreasureRelicAsync(Player player, IReadOnlyList<RelicModel> relics) => Task.FromResult<int?>(0);

    /// <summary>
    /// Travel to the starting map point of an act after the first (native map screen at act start: the starting point is the
    /// only travelable node). Asked before the point is recorded as visited and before its room is entered. Implementations
    /// must return <paramref name="startingPoint"/>. Default: no decision.
    /// </summary>
    Task<MapPoint> ChooseActStartMapPointAsync(MapPoint startingPoint) => Task.FromResult(startingPoint);
}
