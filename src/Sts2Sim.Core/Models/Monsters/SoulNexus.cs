namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SoulNexus : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 254, 234);
    public override int MaxInitialHp => MinInitialHp;
    private int Burn => Value(AscensionLevel.DeadlyEnemies, 31, 29);
    private int Maelstrom => Value(AscensionLevel.DeadlyEnemies, 7, 6);
    private int Drain => Value(AscensionLevel.DeadlyEnemies, 19, 18);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var burn = new MoveState("SOUL_BURN_MOVE", BurnMove, new SingleAttackIntent(Burn));
        var maelstrom = new MoveState("MAELSTROM_MOVE", MaelstromMove, new MultiAttackIntent(Maelstrom, 4));
        var drain = new MoveState("DRAIN_LIFE_MOVE", DrainMove, new SingleAttackIntent(Drain), new DebuffIntent(strong: true));
        var random = new RandomBranchState("RAND");
        burn.FollowUpState = random;
        maelstrom.FollowUpState = random;
        drain.FollowUpState = random;
        random.AddBranch(burn, MoveRepeatType.CannotRepeat);
        random.AddBranch(maelstrom, MoveRepeatType.CannotRepeat);
        random.AddBranch(drain, MoveRepeatType.CannotRepeat);
        return new MonsterMoveStateMachine([burn, maelstrom, drain, random], burn);
    }

    private Task BurnMove(IReadOnlyList<Creature> _) => DamageCmd.Attack(Burn).FromMonster(this).Execute();
    private Task MaelstromMove(IReadOnlyList<Creature> _) => DamageCmd.Attack(Maelstrom).WithHitCount(4).FromMonster(this).Execute();

    private async Task DrainMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Drain).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 2m, Creature, null);
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 2m, Creature, null);
        }
    }

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
