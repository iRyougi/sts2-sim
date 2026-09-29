namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class HauntedShip : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 67, 63);
    public override int MaxInitialHp => MinInitialHp;
    private int SwipeDamage => Value(AscensionLevel.DeadlyEnemies, 14, 13);
    private int StompDamage => Value(AscensionLevel.DeadlyEnemies, 5, 4);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var haunt = new MoveState("HAUNT_MOVE", Haunt, new DebuffIntent(), new StatusIntent(5));
        var swipe = new MoveState("SWIPE_MOVE", Swipe, new SingleAttackIntent(() => SwipeDamage));
        var stomp = new MoveState("STOMP_MOVE", Stomp, new MultiAttackIntent(() => StompDamage, () => 3));
        haunt.FollowUpState = swipe;
        swipe.FollowUpState = stomp;
        stomp.FollowUpState = swipe;
        return new MonsterMoveStateMachine([swipe, stomp, haunt], haunt);
    }

    private async Task Haunt(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 3m, Creature, null);
            for (int index = 0; index < 5; index++)
            {
                var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
                dazed.AssignOwner(target.Player!);
                await CardPileCmd.Generate(Creature.CombatState!, dazed, PileType.Discard, creator: null);
            }
        }
    }

    private Task Swipe(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(SwipeDamage).FromMonster(this).Execute();

    private Task Stomp(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(StompDamage).WithHitCount(3).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
