namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class OwlMagistrate : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 247, 231);
    public override int MaxInitialHp => MinInitialHp;
    private int ScrutinyDamage => Ascension(AscensionLevel.DeadlyEnemies, 17, 16);
    private int PeckDamage => Ascension(AscensionLevel.DeadlyEnemies, 4, 4);
    private int VerdictDamage => Ascension(AscensionLevel.DeadlyEnemies, 36, 33);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var scrutiny = new MoveState("MAGISTRATE_SCRUTINY", Scrutiny, new SingleAttackIntent(ScrutinyDamage));
        var peck = new MoveState("PECK_ASSAULT", Peck, new MultiAttackIntent(PeckDamage, 6));
        var flight = new MoveState("JUDICIAL_FLIGHT", Flight, new BuffIntent());
        var verdict = new MoveState("VERDICT", Verdict, new SingleAttackIntent(VerdictDamage), new DebuffIntent());
        scrutiny.FollowUpState = peck; peck.FollowUpState = flight; flight.FollowUpState = verdict; verdict.FollowUpState = scrutiny;
        return new MonsterMoveStateMachine(new MonsterState[] { scrutiny, peck, flight, verdict }, scrutiny);
    }
    private Task Scrutiny(IReadOnlyList<Creature> targets) => DamageCmd.Attack(ScrutinyDamage).FromMonster(this).Execute();
    private Task Peck(IReadOnlyList<Creature> targets) => DamageCmd.Attack(PeckDamage).WithHitCount(6).FromMonster(this).Execute();
    private Task Flight(IReadOnlyList<Creature> targets) => PowerCmd.Apply<SoarPower>(Creature.CombatState!, Creature, 1m, Creature, null);
    private async Task Verdict(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(VerdictDamage).FromMonster(this).Execute();
        foreach (Creature target in targets) await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 4m, Creature, null);
        SoarPower? soar = Creature.GetPower<SoarPower>();
        if (soar is not null)
        {
            await PowerCmd.Remove(soar);
        }
    }
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
