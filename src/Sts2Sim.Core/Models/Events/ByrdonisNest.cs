using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 nest event. 偏离 #148：省略 DynamicVars/VFX；#150 的 IsAllowed predicate 已在此实现，
/// 事件池选择由 Task 3 集成。已有事件宠物时隐藏 TAKE，避免产生真实游戏不允许的第二枚蛋。</summary>
public sealed class ByrdonisNest : EventModel
{
    public override bool IsAllowed(IRunState runState) => runState.Players.All(player => !player.HasEventPet());

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            new("EAT", EatAsync),
        };
        if (!Owner.HasEventPet())
        {
            options.Add(new EventOption("TAKE", TakeAsync));
        }

        return options;
    }

    private async Task EatAsync()
    {
        await CreatureCmd.GainMaxHp(Owner.Creature, 7m);
        Finish();
    }

    private async Task TakeAsync()
    {
        var egg = (ByrdonisEgg)ModelDb.Card<ByrdonisEgg>().MutableClone();
        egg.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(egg);
        Finish();
    }
}
