using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Entities.Rngs;

namespace Sts2Sim.Core.Rewards;

/// <summary>One relic reward drawn from the player's relic grab bag. 偏离 #70：抽空的稀有度桶直接
/// 兜底 Circlet，不做真实游戏 <c>RelicGrabBag.GetAvailableDeque</c> 那种 Shop→Common→Uncommon→Rare
/// 的跨稀有度降级，与 <see cref="Sts2Sim.Core.Rooms.TreasureRoom"/>/<see cref="Sts2Sim.Core.Entities.Merchant.MerchantInventory"/>
/// 的兜底方式保持一致。</summary>
public sealed class RelicReward : TakeableReward
{
    private readonly RelicModel? _predeterminedRelic;
    private readonly RelicRarity _rarity;

    public RelicModel? Relic { get; private set; }

    public RelicReward(Player player) : base(player)
    {
    }

    public RelicReward(RelicRarity rarity, Player player) : base(player)
    {
        _rarity = rarity;
    }

    public RelicReward(RelicModel relic, Player player) : base(player)
    {
        ArgumentNullException.ThrowIfNull(relic);
        relic.AssertMutable();
        _predeterminedRelic = relic;
        Relic = relic;
    }

    public override void Populate(IRunState runState)
        => Populate(
            runState,
            Player.PlayerRng.ForCurrentScopeOrSemanticKey(
                PlayerRngType.Rewards,
                $"{Player.CurrentSemanticLocationKey}/reward/slot=relic"));

    internal void Populate(IRunState runState, Rng rng)
    {
        if (_predeterminedRelic is not null)
        {
            Relic = _predeterminedRelic;
            return;
        }

        RelicRarity rarity = _rarity == RelicRarity.None
            ? RelicFactory.RollRarity(rng) : _rarity;
        Relic = RelicFactory.PullNextRelicFromFront(Player, rarity);
    }

    protected override Task OnTake()
    {
        return Relic is null ? Task.CompletedTask : RelicCmd.Obtain(Relic, Player);
    }
}
