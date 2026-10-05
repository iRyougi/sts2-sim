using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Runs;

/// <summary>战斗与战斗内选牌交给搜索，其余全部交给战略决策源。</summary>
public sealed class CombinedRunDecisionSource : IRunDecisionSource, ICombatCompletionValidator
{
    private readonly IRunDecisionSource _combatSource;
    private readonly IRunDecisionSource _strategySource;

    public CombinedRunDecisionSource(
        IRunDecisionSource combatSource,
        IRunDecisionSource strategySource)
    {
        _combatSource = combatSource ?? throw new ArgumentNullException(nameof(combatSource));
        _strategySource = strategySource ?? throw new ArgumentNullException(nameof(strategySource));
    }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        _strategySource.ChooseMapPointAsync(options);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        _combatSource.ChooseCombatActionAsync(state);

    public void ValidateCompletedCombat(CombatState state) =>
        (_combatSource as ICombatCompletionValidator)?.ValidateCompletedCombat(state);

    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
        request.IsLiveCombatSelection
            ? _combatSource.ChooseCardsAsync(request)
            : _strategySource.ChooseCardsAsync(request);

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards) =>
        _strategySource.ChooseRewardActionAsync(rewards);

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player) =>
        _strategySource.ChooseShopActionAsync(inventory, player);

    public Task<CustomEventDecision> ChooseCustomEventActionAsync(EventModel @event) =>
        _strategySource.ChooseCustomEventActionAsync(@event);

    public Task<RestSiteDecision> ChooseRestSiteActionAsync(
        Player player,
        IReadOnlyList<RestSiteDecision> candidates) =>
        _strategySource.ChooseRestSiteActionAsync(player, candidates);

    public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options) =>
        _strategySource.ChooseEventOptionAsync(options);
}
