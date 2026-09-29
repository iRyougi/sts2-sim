using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class Trial : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("ACCEPT", AcceptAsync),
        new EventOption("REJECT", RejectAsync),
    ];

    private Task AcceptAsync()
    {
        SetOptions(Rng.NextInt(3) switch
        {
            0 => VerdictOptions("MERCHANT", MerchantGuiltyAsync, MerchantInnocentAsync),
            1 => VerdictOptions("NOBLE", NobleGuiltyAsync, NobleInnocentAsync),
            _ => VerdictOptions("NONDESCRIPT", NondescriptGuiltyAsync, NondescriptInnocentAsync),
        });
        return Task.CompletedTask;
    }

    private Task RejectAsync()
    {
        SetOptions(
        [
            new EventOption("ACCEPT", AcceptAsync),
            new EventOption("DOUBLE_DOWN", DoubleDownAsync),
        ]);
        return Task.CompletedTask;
    }

    /// <summary>偏离 #252：原版 Double Down 先显示确认弹窗；无头事件 API 没有弹窗阶段，
    /// 所以选中该选项后直接走不可防止的放弃运行死亡路径。</summary>
    private async Task DoubleDownAsync()
    {
        await CreatureCmd.KillUnpreventably(RunState, Owner.Creature);
        Finish();
    }

    private async Task MerchantGuiltyAsync()
    {
        await AddCurseAsync<Regret>();
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner), Owner);
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner), Owner);
        Finish();
    }

    private async Task MerchantInnocentAsync()
    {
        await AddCurseAsync<Shame>();
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            RunState,
            Owner,
            Owner.Deck.Cards.Where(card => card.IsUpgradable),
            2,
            2,
            this);
        foreach (CardModel card in selected)
        {
            CardCmd.Upgrade(card);
        }
        Finish();
    }

    private async Task NobleGuiltyAsync()
    {
        await CreatureCmd.Heal(Owner.Creature, 10m);
        Finish();
    }

    private async Task NobleInnocentAsync()
    {
        await AddCurseAsync<Regret>();
        await PlayerCmd.GainGold(300m, Owner);
        Finish();
    }

    /// <summary>偏离 #253：原版同一奖励房间包含两个独立三选一卡牌奖励；本仓库的
    /// RewardsSet 只有一个 Card 槽，因此依原顺序排入两个待领取 RewardsSet。</summary>
    private async Task NondescriptGuiltyAsync()
    {
        await AddCurseAsync<Doubt>();
        for (int i = 0; i < 2; i++)
        {
            var reward = new CardReward(Owner, CardRarityOddsType.RegularEncounter);
            reward.Populate(RunState);
            OfferRewards(RewardsSet.CreateCustom(Owner, card: reward));
        }
        Finish();
    }

    private async Task NondescriptInnocentAsync()
    {
        await AddCurseAsync<Doubt>();
        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            RunState,
            Owner,
            Owner.Deck.Cards.Where(card => card.IsTransformable),
            2,
            2,
            this);
        foreach (CardModel card in selected)
        {
            await CardCmd.TransformToRandom(card, Rng, RunState);
        }
        Finish();
    }

    private static IReadOnlyList<EventOption> VerdictOptions(
        string prefix,
        Func<Task> guilty,
        Func<Task> innocent) =>
    [
        new EventOption($"{prefix}_GUILTY", guilty),
        new EventOption($"{prefix}_INNOCENT", innocent),
    ];

    private async Task AddCurseAsync<TCard>() where TCard : CardModel
    {
        var curse = (CardModel)ModelDb.Card<TCard>().MutableClone();
        curse.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(curse);
    }
}
