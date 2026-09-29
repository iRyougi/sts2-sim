namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>偏离 #181：真实源码里"死亡动画是否播放"跟随主怪 Fogmog 是否还活着（纯 Spine 动画判定，
/// <c>NCreature.Dead</c> 触发条件），本项目无渲染层，不移植这条视觉同步逻辑；玩法层面的死亡/清场判定
/// 由 <see cref="Powers.IllusionPower"/>/<see cref="Powers.MinionPower"/> 已实现的次要敌人生命周期覆盖。</summary>
public sealed class EyeWithTeeth : MonsterModel
{
    private const int DistractAmount = 3;

    public override int MinInitialHp => 6;

    public override int MaxInitialHp => MinInitialHp;

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<IllusionPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var distract = new MoveState(
            "DISTRACT_MOVE",
            DistractMove,
            new StatusIntent(DistractAmount));
        distract.FollowUpState = distract;
        return new MonsterMoveStateMachine(new MonsterState[] { distract }, distract);
    }

    private async Task DistractMove(IReadOnlyList<Creature> targets)
    {
        var combatState = Creature.CombatState
            ?? throw new InvalidOperationException("Distract requires active combat.");
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            for (int index = 0; index < DistractAmount; index++)
            {
                var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
                dazed.AssignOwner(target.Player!);
                await CardPileCmd.Generate(combatState, dazed, PileType.Discard, creator: null);
            }
        }
    }
}
