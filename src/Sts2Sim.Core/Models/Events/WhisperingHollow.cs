using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 hollow event. 偏离 #82/#148：省略 UI/DynamicVars；#150 的 IsAllowed predicate 已在此实现，
/// 事件池选择由 Task 3 集成。低金币时选项效果仍通过 LoseGold 的夹取语义支付。</summary>
public sealed class WhisperingHollow : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Gold >= 44m);

    private decimal _cost;

    protected override void CalculateVars() => _cost = 35m + Rng.NextInt(-9, 10);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("GOLD", GoldAsync),
        new EventOption("HUG", HugAsync),
    };

    private async Task GoldAsync()
    {
        await PlayerCmd.LoseGold(_cost, Owner, GoldLossType.Spent);
        var first = new PotionReward(Owner);
        first.Populate(RunState);
        var second = new PotionReward(Owner);
        second.Populate(RunState);
        OfferRewards(RewardsSet.CreateCustom(
            Owner,
            potion: first,
            extraRewards: new Reward[] { second }));
        Finish();
    }

    private async Task HugAsync()
    {
        CardModel? selected = (await CardSelectCmd.FromDeckForTransformation(Owner, 1, this)).FirstOrDefault();
        if (selected is not null)
        {
            await CardCmd.TransformToRandom(selected, Rng, RunState);
        }

        await CreatureCmd.Damage(
            RunState,
            Owner.Creature,
            9m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        Finish();
    }
}
