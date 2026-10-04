namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

/// <summary>偏离 #180：真实源码构建了一个 <c>SLEEP_MOVE_2</c> 状态但从未接入任何 FollowUp 链或初始态，
/// 权威运行时永远不可达，本项目不移植这个孤立状态。</summary>
public sealed class BygoneEffigy : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 132, 127);

    public override int MaxInitialHp => MinInitialHp;

    private int SlashDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 15, 13);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<SlowPower>(
            Creature.CombatState!,
            Creature,
            1m,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var sleep = new MoveState("SLEEP_MOVE", InitialSleepMove, new SleepIntent());
        var wake = new MoveState("WAKE_MOVE", WakeMove, new BuffIntent());
        var slashes = new MoveState(
            "SLASHES_MOVE",
            SlashMove,
            new SingleAttackIntent(SlashDamage));
        sleep.FollowUpState = wake;
        wake.FollowUpState = slashes;
        slashes.FollowUpState = slashes;
        return new MonsterMoveStateMachine(new MonsterState[] { sleep, wake, slashes }, sleep);
    }

    private Task InitialSleepMove(IReadOnlyList<Creature> targets) => Task.CompletedTask;

    private async Task WakeMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            10m,
            Creature,
            cardSource: null);
    }

    private async Task SlashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SlashDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
