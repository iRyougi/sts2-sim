using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

/// <summary>v0.113.1 shared Act 2 event (#927).
/// 偏离（#927，见偏离登记册）：原版在 CalculateVars 用事件 Rng 抽取陈列遗物；该 Rng 在本事件只有这一个消费者，
/// 模拟器推迟到 DISPLAY 页打开时抽取，顺序模式逐位相同，避免重播种克隆在揭示前继承尚未公开的遗物。</summary>
public sealed class MuseumOfTorture : EventModel
{
    private const decimal BreakOutMaxHpLoss = 10m;

    private RelicModel? _offeredRelic;

    public RelicModel? OfferedRelic => _offeredRelic;

    protected override VisibleDecisionMetadata BuildVisibleState() => _offeredRelic is null
        ? new([], [new("CONTINUE", [])])
        : new([], [
            new("TAKE", [VisibleValue.Id("Relic", _offeredRelic.Id.Entry), VisibleValue.Int("RelicCount", 1),
                VisibleValue.Int("CurseCount", 1), VisibleValue.Condition("Curse", "Random")])
                { Contents = [VisibleContentProjection.ForRelic(_offeredRelic)] },
            new("BREAK_OUT", [VisibleValue.Int("MaxHpLoss", (int)BreakOutMaxHpLoss)]),
        ]);

    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: 1 };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new EventOption("CONTINUE", EnterMuseum)];

    private Task EnterMuseum()
    {
        SetOptions([new EventOption("CONTINUE", ViewDisplay)]);
        return Task.CompletedTask;
    }

    private Task ViewDisplay()
    {
        _offeredRelic = Rng.NextItem(new RelicModel[]
        {
            ModelDb.Relic<ObsidianVice>(),
            ModelDb.Relic<Thumbscrew>(),
            ModelDb.Relic<TormentousCollar>(),
        });
        SetOptions([
            new EventOption("TAKE", TakeRelic),
            new EventOption("BREAK_OUT", BreakOut),
        ]);
        return Task.CompletedTask;
    }

    private async Task TakeRelic()
    {
        RelicModel relic = _offeredRelic ?? throw new InvalidOperationException("Museum relic has not been displayed.");
        await RelicCmd.Obtain(relic, Owner);
        List<CardModel> curses = CurseCardPool.Instance.GetUnlockedCards(Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .Where(card => card.CanBeGeneratedByModifiers)
            .OrderBy(card => card.Id)
            .ToList();
        CardModel canonical = Owner.PlayerRng.NonCombatGeneration.NextItem(curses)
            ?? throw new InvalidOperationException("No eligible curse is available for MuseumOfTorture.");
        var curse = (CardModel)canonical.MutableClone();
        curse.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(curse);
        Finish();
    }

    private async Task BreakOut()
    {
        await CreatureCmd.LoseMaxHp(RunState, Owner.Creature, BreakOutMaxHpLoss, cardSource: null);
        Finish();
    }
}
