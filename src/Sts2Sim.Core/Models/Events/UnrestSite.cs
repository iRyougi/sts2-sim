using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 event offering complete rest with a curse or a max-HP sacrifice for a relic.
/// 偏离 #148：不移植 DynamicVars，使用源码常量 8；#150 的 IsAllowed predicate 已在此实现，事件池选择由 Task 3 集成。</summary>
public sealed class UnrestSite : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Creature.CurrentHp <= player.Creature.MaxHp * 0.70m);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("REST", RestAsync),
        new EventOption("KILL", KillAsync),
    };

    private async Task RestAsync()
    {
        await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<PoorSleep>() }, Owner);
        Finish();
    }

    private async Task KillAsync()
    {
        await CreatureCmd.LoseMaxHp(RunState, Owner.Creature, 8m, isFromCard: false);
        RelicModel relic = RelicFactory.PullNextRelicFromFront(Owner);
        await RelicCmd.Obtain(relic, Owner);
        Finish();
    }
}
