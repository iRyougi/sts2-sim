namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class GasBomb : MonsterModel
{
    private bool _hasExploded;
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 8, 7);
    public override int MaxInitialHp => MinInitialHp;

    private bool HasExploded
    {
        get => _hasExploded;
        set
        {
            AssertMutable();
            _hasExploded = value;
        }
    }
    private int ExplosionDamage => Value(AscensionLevel.DeadlyEnemies, 9, 8);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<MinionPower>(Creature.CombatState!, Creature, 1m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var explode = new MoveState("EXPLODE_MOVE", Explode, new DeathBlowIntent(() => ExplosionDamage));
        return new MonsterMoveStateMachine([explode], explode);
    }

    private async Task Explode(IReadOnlyList<Creature> _)
    {
        HasExploded = true;
        await DamageCmd.Attack(ExplosionDamage).FromMonster(this).Execute();
        await CreatureCmd.Kill(Creature);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_hasExploded);

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
