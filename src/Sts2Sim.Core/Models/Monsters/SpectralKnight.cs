namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SpectralKnight : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 97, 93);
    public override int MaxInitialHp => MinInitialHp;
    private int Slash => Value(AscensionLevel.DeadlyEnemies, 17, 15);
    private int Flame => Value(AscensionLevel.DeadlyEnemies, 4, 3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var hex = new MoveState("HEX", Hex, new DebuffIntent());
        var slash = new MoveState("SOUL_SLASH", SlashMove, new SingleAttackIntent(Slash));
        var flame = new MoveState("SOUL_FLAME", FlameMove, new MultiAttackIntent(Flame, 3));
        var random = new RandomBranchState("RAND");
        hex.FollowUpState = slash;
        slash.FollowUpState = random;
        flame.FollowUpState = random;
        random.AddBranch(slash, 2);
        random.AddBranch(flame, MoveRepeatType.CannotRepeat);
        return new MonsterMoveStateMachine([hex, slash, flame, random], hex);
    }

    private async Task Hex(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<HexPower>(Creature.CombatState!, target, 2m, Creature, null);
        }
    }

    private Task SlashMove(IReadOnlyList<Creature> _) => DamageCmd.Attack(Slash).FromMonster(this).Execute();
    private Task FlameMove(IReadOnlyList<Creature> _) => DamageCmd.Attack(Flame).WithHitCount(3).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
