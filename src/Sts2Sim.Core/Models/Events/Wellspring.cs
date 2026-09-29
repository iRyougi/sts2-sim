using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 spring event. 偏离 #82/#148：牌组移除经决策源，省略 UI/DynamicVars；
/// 偏离 #164：模拟器没有角色/共享药水池与解锁状态，BOTTLE 从已注册的非 Event C/U/R 药水扁平池均匀抽取。</summary>
public sealed class Wellspring : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("BOTTLE", BottleAsync),
        new EventOption("BATHE", BatheAsync),
    };

    private Task BottleAsync()
    {
        // 逐条对照上游 Wellspring.Bottle：候选是 Character.PotionPool ∪ SharedPotionPool，
        // 与 PotionFactory.GetOutOfCombatPool 同源。此前用 ModelDb.All<PotionModel>() 按稀有度
        // 过滤的扁平池，会把别的角色的专属药水发给当前角色，也绕过解锁门禁（偏离 #301）。
        IReadOnlyList<PotionModel> eligible = PotionFactory.GetOutOfCombatPool(Owner);
        PotionModel? canonical = Owner.PlayerRng.Rewards.NextItem(eligible);
        if (canonical is not null)
        {
            var reward = new PotionReward((PotionModel)canonical.MutableClone(), Owner);
            OfferRewards(RewardsSet.CreateCustom(Owner, potion: reward));
        }
        Finish();
        return Task.CompletedTask;
    }

    private async Task BatheAsync()
    {
        CardModel? selected = (await CardSelectCmd.FromDeckForRemoval(Owner, 1, this)).FirstOrDefault();
        if (selected is not null)
        {
            await CardPileCmd.RemoveFromDeck(Owner, selected);
        }

        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Guilty>() }, Owner);
        Finish();
    }
}
