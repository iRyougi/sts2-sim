namespace Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;
public sealed class MagiKnight : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 89, 82);
    public override int MaxInitialHp => MinInitialHp;
    private int ShieldDamage => Value(AscensionLevel.DeadlyEnemies, 7, 6);
    private int ShieldBlock => Value(AscensionLevel.ToughEnemies, 9, 5);
    private int RamDamage => Value(AscensionLevel.DeadlyEnemies, 11, 10);
    private int BombDamage => Value(AscensionLevel.DeadlyEnemies, 40, 35);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var shield = new MoveState("POWER_SHIELD_MOVE", Shield, new SingleAttackIntent(ShieldDamage), new DefendIntent());
        var dampen = new MoveState("DAMPEN_MOVE", Dampen, new DebuffIntent());
        var ram = new MoveState("RAM_MOVE", Ram, new SingleAttackIntent(RamDamage));
        var prep = new MoveState("PREP_MOVE", Prep, new DefendIntent());
        var bomb = new MoveState("MAGIC_BOMB", Bomb, new SingleAttackIntent(BombDamage));
        shield.FollowUpState = dampen;
        dampen.FollowUpState = ram;
        ram.FollowUpState = prep;
        prep.FollowUpState = bomb;
        bomb.FollowUpState = ram;
        return new MonsterMoveStateMachine([shield, dampen, ram, prep, bomb], shield);
    }
    private async Task Dampen(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            DampenPower? power = target.GetPower<DampenPower>();
            if (power is null)
            {
                await PowerCmd.Apply<DampenPower>(Creature.CombatState!, target, 1m, Creature, null);
                power = target.GetPower<DampenPower>();
            }

            power?.AddCaster(Creature);
        }
    }

    private async Task Shield(IReadOnlyList<Creature> _)
    {
        await DamageCmd.Attack(ShieldDamage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, ShieldBlock, ValueProp.Move, null, null);
    }

    private Task Ram(IReadOnlyList<Creature> _) => DamageCmd.Attack(RamDamage).FromMonster(this).Execute();

    private Task Prep(IReadOnlyList<Creature> _) =>
        CreatureCmd.GainBlock(Creature.CombatState!, Creature, ShieldBlock, ValueProp.Move, null, null);

    private Task Bomb(IReadOnlyList<Creature> _) => DamageCmd.Attack(BombDamage).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
