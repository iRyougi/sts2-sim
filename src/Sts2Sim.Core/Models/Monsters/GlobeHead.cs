namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class GlobeHead : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 158, 148);
    public override int MaxInitialHp => MinInitialHp;
    private int ThunderDamage => Ascension(AscensionLevel.DeadlyEnemies, 7, 6);
    private int SlapDamage => Ascension(AscensionLevel.DeadlyEnemies, 14, 13);
    private int BurstDamage => Ascension(AscensionLevel.DeadlyEnemies, 17, 16);
    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<GalvanicPower>(Creature.CombatState!, Creature, Ascension(AscensionLevel.DeadlyEnemies, 8, 6), Creature, null);
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var thunder = new MoveState("THUNDER_STRIKE", Thunder, new MultiAttackIntent(ThunderDamage, 3));
        var slap = new MoveState("SHOCKING_SLAP", Slap, new SingleAttackIntent(SlapDamage), new DebuffIntent());
        var burst = new MoveState("GALVANIC_BURST", Burst, new SingleAttackIntent(BurstDamage), new BuffIntent());
        slap.FollowUpState = thunder; thunder.FollowUpState = burst; burst.FollowUpState = slap;
        return new MonsterMoveStateMachine(new MonsterState[] { thunder, slap, burst }, slap);
    }
    private Task Thunder(IReadOnlyList<Creature> targets) => DamageCmd.Attack(ThunderDamage).WithHitCount(3).FromMonster(this).Execute();
    private async Task Slap(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SlapDamage).FromMonster(this).Execute();
        foreach (Creature target in targets) await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 2m, Creature, null);
    }
    private async Task Burst(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BurstDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
