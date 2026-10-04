using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.MonsterMoves;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>亡灵契约师的召唤物。只由 <c>OstyCmd.Summon</c> 作为玩家宠物加入战斗；生命值由召唤量决定，
/// 这里的 1/1 只是创建时的初始值。死后因 <c>DieForYouPower</c> 留在战斗里，可被再次召唤复活。</summary>
public sealed class Osty : MonsterModel
{
    public override int MinInitialHp => 1;

    public override int MaxInitialHp => 1;

    public override bool IsHealthBarVisible =>
        (Creature ?? throw new InvalidOperationException("Creature was accessed before it was set.")).IsAlive;

    // 原版先震动 Godot Osty 节点；headless 省略该视觉调用，保留真实缺失判断。
    public static bool CheckMissingWithAnim(Player owner) => owner.IsOstyMissing;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("NOTHING_MOVE", _ => Task.CompletedTask);
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }
}
