using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class Trial : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("DOUBLE_DOWN", [VisibleValue.Condition("Lethal", "true")]),
        new("MERCHANT_GUILTY", [VisibleValue.Id("Curse", ModelDb.GetId<Regret>().Entry), VisibleValue.Int("CurseCount", 1), VisibleValue.Int("RelicCount", 2), VisibleValue.Condition("Relic", "Random")])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Regret>())] },
        new("MERCHANT_INNOCENT", [VisibleValue.Id("Curse", ModelDb.GetId<Shame>().Entry), VisibleValue.Int("CurseCount", 1), VisibleValue.Int("UpgradeCount", 2)])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Shame>())] },
        new("NOBLE_GUILTY", [VisibleValue.Int("Heal", 10)]),
        new("NOBLE_INNOCENT", [VisibleValue.Id("Curse", ModelDb.GetId<Regret>().Entry), VisibleValue.Int("CurseCount", 1), VisibleValue.Int("Gold", 300)])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Regret>())] },
        new("NONDESCRIPT_GUILTY", [VisibleValue.Id("Curse", ModelDb.GetId<Doubt>().Entry), VisibleValue.Int("CurseCount", 1), VisibleValue.Int("CardRewardCount", 2), VisibleValue.Condition("CardReward", "Random")])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Doubt>())] },
        new("NONDESCRIPT_INNOCENT", [VisibleValue.Id("Curse", ModelDb.GetId<Doubt>().Entry), VisibleValue.Int("CurseCount", 1), VisibleValue.Int("TransformCount", 2), VisibleValue.Condition("TransformResult", "Random")])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Doubt>())] },
    ])
    {
        // #637: narrative entrant number is absent in Core; metadata must not draw Chaotic RNG.
        CoverageGapIds = CurrentOptions.Any(option => option.Key == "REJECT") ? ["G1.TrialNarrative"] : [],
    };

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

    /// <summary>#1039（销 #253）：同一自定义奖励屏上的两个三选一卡牌奖励，经 <see cref="RewardsCmd.OfferCustom"/>
    /// 依序生成（原版 <c>Trial.NondescriptGuilty</c>：<c>ForNonCombatWithDefaultOdds</c>、三张）。</summary>
    private async Task NondescriptGuiltyAsync()
    {
        await AddCurseAsync<Doubt>();
        var rewards = new List<Reward>(2);
        for (int i = 0; i < 2; i++)
        {
            rewards.Add(new CardReward(Owner,
                CardCreationOptions.ForNonCombatWithDefaultOdds([Owner.Character.CardPool]), 3));
        }
        await RewardsCmd.OfferCustom(Owner, rewards);
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
