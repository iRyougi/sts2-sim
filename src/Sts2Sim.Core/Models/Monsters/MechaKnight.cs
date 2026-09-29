namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class MechaKnight : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 320, 300);
    public override int MaxInitialHp => MinInitialHp;
    private int ChargeDamage => Ascension(AscensionLevel.DeadlyEnemies, 30, 25);
    private int FlamethrowerDamage => Ascension(AscensionLevel.DeadlyEnemies, 12, 8);
    private int CleaveDamage => Ascension(AscensionLevel.DeadlyEnemies, 40, 35);
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<ArtifactPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var charge = new MoveState("CHARGE_MOVE", Charge, new SingleAttackIntent(ChargeDamage));
        var flame = new MoveState("FLAMETHROWER_MOVE", Flamethrower, new SingleAttackIntent(FlamethrowerDamage), new StatusIntent(4));
        var windup = new MoveState("WINDUP_MOVE", Windup, new DefendIntent(), new BuffIntent());
        var cleave = new MoveState("HEAVY_CLEAVE_MOVE", Cleave, new SingleAttackIntent(CleaveDamage));
        charge.FollowUpState = flame; flame.FollowUpState = windup; windup.FollowUpState = cleave; cleave.FollowUpState = flame;
        return new MonsterMoveStateMachine(new MonsterState[] { charge, flame, windup, cleave }, charge);
    }
    private Task Charge(IReadOnlyList<Creature> targets) => DamageCmd.Attack(ChargeDamage).FromMonster(this).Execute();
    private async Task Flamethrower(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(FlamethrowerDamage).FromMonster(this).Execute();
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            for (int index = 0; index < 4; index++)
            {
                var burn = (Burn)ModelDb.Card<Burn>().MutableClone();
                burn.AssignOwner(target.Player!);
                await CardPileCmd.Generate(Creature.CombatState!, burn, PileType.Hand, creator: null);
            }
        }
    }
    private async Task Windup(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, 15m, ValueProp.Move, null, null);
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 5m, Creature, null);
    }
    private Task Cleave(IReadOnlyList<Creature> targets) => DamageCmd.Attack(CleaveDamage).FromMonster(this).Execute();
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
