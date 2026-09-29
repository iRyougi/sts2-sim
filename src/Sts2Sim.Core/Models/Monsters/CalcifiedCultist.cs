namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class CalcifiedCultist : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 39, 38);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 42, 41);
    private int DarkStrikeDamage => Value(AscensionLevel.DeadlyEnemies, 11, 9);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var incantation = new MoveState("INCANTATION_MOVE", Incantation, new BuffIntent());
        var darkStrike = new MoveState("DARK_STRIKE_MOVE", DarkStrike, new SingleAttackIntent(() => DarkStrikeDamage));
        incantation.FollowUpState = darkStrike;
        darkStrike.FollowUpState = darkStrike;
        return new MonsterMoveStateMachine([incantation, darkStrike], incantation);
    }

    private Task Incantation(IReadOnlyList<Creature> _) =>
        PowerCmd.Apply<RitualPower>(Creature.CombatState!, Creature, 2m, Creature, null);

    private Task DarkStrike(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(DarkStrikeDamage).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
