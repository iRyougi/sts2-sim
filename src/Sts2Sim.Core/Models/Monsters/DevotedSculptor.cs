namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class DevotedSculptor : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 172, 162);
    public override int MaxInitialHp => MinInitialHp;
    private int SavageDamage => Ascension(AscensionLevel.DeadlyEnemies, 15, 12);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var incantation = new MoveState("FORBIDDEN_INCANTATION_MOVE", Incantation, new BuffIntent());
        var savage = new MoveState("SAVAGE_MOVE", Savage, new SingleAttackIntent(SavageDamage));
        incantation.FollowUpState = savage;
        savage.FollowUpState = savage;
        return new MonsterMoveStateMachine(new MonsterState[] { incantation, savage }, incantation);
    }
    private Task Incantation(IReadOnlyList<Creature> targets) => PowerCmd.Apply<RitualPower>(Creature.CombatState!, Creature, 9m, null, null);
    private Task Savage(IReadOnlyList<Creature> targets) => DamageCmd.Attack(SavageDamage).FromMonster(this).Execute();
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
