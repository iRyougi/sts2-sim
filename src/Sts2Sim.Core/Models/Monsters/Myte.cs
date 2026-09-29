namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Myte : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 64, 61);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 69, 67);
    private int BiteDamage => Ascension(AscensionLevel.DeadlyEnemies, 15, 13);
    private int SuckDamage => Ascension(AscensionLevel.DeadlyEnemies, 6, 4);
    private int SuckStrength => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var toxic = new MoveState("TOXIC_MOVE", ToxicMove, new StatusIntent(2));
        var bite = new MoveState("BITE_MOVE", _ => DamageCmd.Attack(BiteDamage).FromMonster(this).Execute(), new SingleAttackIntent(BiteDamage));
        var suck = new MoveState("SUCK_MOVE", Suck, new SingleAttackIntent(SuckDamage), new BuffIntent());
        toxic.FollowUpState = bite;
        bite.FollowUpState = suck;
        suck.FollowUpState = toxic;
        var initial = new ConditionalBranchState("INIT_MOVE")
            .AddBranch(creature => creature.SlotName == "second", suck.Id)
            .AddBranch(_ => true, toxic.Id);
        return new MonsterMoveStateMachine([initial, toxic, bite, suck], initial);
    }

    private async Task ToxicMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        for (int i = 0; i < 2; i++)
        {
            var toxic = (Toxic)ModelDb.Card<Toxic>().MutableClone();
            toxic.AssignOwner(target.Player!);
            await CardPileCmd.Generate(Creature.CombatState!, toxic, PileType.Hand, creator: null);
        }
    }

    private async Task Suck(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(SuckDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, SuckStrength, Creature, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
