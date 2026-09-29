namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TheLost : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 99, 93);
    public override int MaxInitialHp => MinInitialHp;
    private int Lasers => Value(AscensionLevel.DeadlyEnemies, 5, 4);

    public override Task BeforeCombatStart() =>
        PowerCmd.Apply<PossessStrengthPower>(Creature.CombatState!, Creature, 1m, null, null);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var smog = new MoveState("DEBILITATING_SMOG", Smog, new DebuffIntent(), new BuffIntent());
        var lasers = new MoveState("EYE_LASERS", Laser, new MultiAttackIntent(Lasers, 2));
        smog.FollowUpState = lasers;
        lasers.FollowUpState = smog;
        return new MonsterMoveStateMachine([smog, lasers], smog);
    }

    private async Task Smog(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, target, -2m, Creature, null);
        }

        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }

    private Task Laser(IReadOnlyList<Creature> _) => DamageCmd.Attack(Lasers).WithHitCount(2).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
