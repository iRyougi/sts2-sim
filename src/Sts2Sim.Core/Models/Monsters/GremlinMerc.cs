namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class GremlinMerc : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 51, 47);
    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 53, 49);
    private int GimmeDamage => Value(AscensionLevel.ToughEnemies, 8, 7);
    private int DoubleSmashDamage => Value(AscensionLevel.ToughEnemies, 7, 6);
    private int HeheDamage => Value(AscensionLevel.ToughEnemies, 9, 8);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        ICombatState combatState = Creature.CombatState!;
        await PowerCmd.Apply<SurprisePower>(combatState, Creature, 1m, Creature, null);
        foreach (Creature player in combatState.Allies.Where(creature => creature.Player is not null))
        {
            ThieveryPower? thievery = await PowerCmd.Apply<ThieveryPower>(
                combatState,
                Creature,
                20m,
                Creature,
                null);
            if (thievery is not null)
            {
                thievery.Target = player;
            }
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var gimme = new MoveState("GIMME_MOVE", Gimme, new MultiAttackIntent(() => GimmeDamage, () => 2));
        var smash = new MoveState("DOUBLE_SMASH_MOVE", DoubleSmash, new MultiAttackIntent(() => DoubleSmashDamage, () => 2), new DebuffIntent());
        var hehe = new MoveState("HEHE_MOVE", Hehe, new SingleAttackIntent(() => HeheDamage), new BuffIntent());
        gimme.FollowUpState = smash;
        smash.FollowUpState = hehe;
        hehe.FollowUpState = gimme;
        return new MonsterMoveStateMachine([gimme, smash, hehe], gimme);
    }

    private async Task Gimme(IReadOnlyList<Creature> _)
    {
        await DamageCmd.Attack(GimmeDamage).WithHitCount(2).FromMonster(this).Execute();
        await Steal();
    }

    private async Task DoubleSmash(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(DoubleSmashDamage).WithHitCount(2).FromMonster(this).Execute();
        await Steal();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 2m, Creature, null);
        }
    }

    private async Task Hehe(IReadOnlyList<Creature> _)
    {
        await DamageCmd.Attack(HeheDamage).FromMonster(this).Execute();
        await Steal();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }
    private async Task Steal()
    {
        foreach (ThieveryPower thievery in Creature.Powers.OfType<ThieveryPower>())
        {
            await thievery.Steal();
        }
    }

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
