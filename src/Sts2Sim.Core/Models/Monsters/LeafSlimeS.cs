namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class LeafSlimeS : MonsterModel
{
    private const int GoopAmount = 1;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 12, 11);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 16, 15);

    private int TackleDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 4, 3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var tackle = new MoveState(
            "TACKLE_MOVE",
            TackleMove,
            new SingleAttackIntent(TackleDamage));
        var goop = new MoveState(
            "GOOP_MOVE",
            GoopMove,
            new StatusIntent(GoopAmount));
        var random = new RandomBranchState("RAND");
        random.AddBranch(tackle, MoveRepeatType.CannotRepeat, 1f);
        random.AddBranch(goop, MoveRepeatType.CannotRepeat, 1f);
        tackle.FollowUpState = random;
        goop.FollowUpState = random;
        return new MonsterMoveStateMachine(
            new MonsterState[] { tackle, goop, random },
            random);
    }

    private async Task TackleMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TackleDamage).FromMonster(this).Execute();
    }

    private async Task GoopMove(IReadOnlyList<Creature> targets)
    {
        var combatState = Creature.CombatState
            ?? throw new InvalidOperationException("Goop requires active combat.");
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            var slimed = (Slimed)ModelDb.Card<Slimed>().MutableClone();
            slimed.AssignOwner(target.Player!);
            await CardPileCmd.Generate(combatState, slimed, PileType.Discard, creator: null);
        }
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
