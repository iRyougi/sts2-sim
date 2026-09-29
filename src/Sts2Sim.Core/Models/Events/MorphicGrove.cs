using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Events;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 event offering a shared sacrifice or solitary vitality. 偏离 #148：不移植 DynamicVars
/// 容器，直接使用玩法常量；#150 的 IsAllowed predicate 已在此实现，事件池选择由 Task 3 集成。</summary>
public sealed class MorphicGrove : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Gold >= 100m &&
                                       player.Deck.Cards.Count(card => card.IsTransformable) >= 2);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("GROUP", GroupAsync),
        new EventOption("LONER", LonerAsync),
    };

    private async Task GroupAsync()
    {
        await PlayerCmd.LoseGold(Owner.Gold, Owner, GoldLossType.Stolen);
        foreach (CardModel card in (await CardSelectCmd.FromDeckForTransformation(Owner, 2, this)))
        {
            await CardCmd.TransformToRandom(card, Rng, RunState);
        }

        Finish();
    }

    private async Task LonerAsync()
    {
        await CreatureCmd.GainMaxHp(Owner.Creature, 5m);
        Finish();
    }
}
