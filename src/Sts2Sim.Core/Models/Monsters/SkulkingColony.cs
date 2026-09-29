namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SkulkingColony : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 80, 75);
    public override int MaxInitialHp => MinInitialHp;

    private int InertiaDamage => Ascension(AscensionLevel.DeadlyEnemies, 11, 9);
    private int ZoomDamage => Ascension(AscensionLevel.DeadlyEnemies, 16, 14);
    private int PiercingStabsDamage => Ascension(AscensionLevel.DeadlyEnemies, 8, 7);
    private int InertiaStrength => Ascension(AscensionLevel.DeadlyEnemies, 4, 2);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<HardenedShellPower>(Creature.CombatState!, Creature, 20m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var zoom = new MoveState("ZOOM_MOVE", Zoom, new SingleAttackIntent(() => ZoomDamage));
        var zoom2 = new MoveState("ZOOM_MOVE_2", Zoom, new SingleAttackIntent(() => ZoomDamage));
        var inertia = new MoveState(
            "INERTIA_MOVE",
            Inertia,
            new SingleAttackIntent(() => InertiaDamage),
            new BuffIntent());
        var stabs = new MoveState(
            "PIERCING_STABS_MOVE",
            PiercingStabs,
            new MultiAttackIntent(() => PiercingStabsDamage, () => 2));
        zoom.FollowUpState = zoom2;
        zoom2.FollowUpState = inertia;
        inertia.FollowUpState = stabs;
        stabs.FollowUpState = zoom;
        return new MonsterMoveStateMachine([zoom, zoom2, inertia, stabs], zoom);
    }

    private Task Zoom(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(ZoomDamage).FromMonster(this).Execute();

    private async Task Inertia(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(InertiaDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, InertiaStrength, Creature, null);
    }

    private Task PiercingStabs(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(PiercingStabsDamage).WithHitCount(2).FromMonster(this).Execute();

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
