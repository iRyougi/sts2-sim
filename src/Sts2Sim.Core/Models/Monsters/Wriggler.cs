using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Monsters;

/// <summary>Wriggler supporting both authoritative slot branching and the Plan06e DenseVegetation
/// no-slot parity fallback. HP and Bite damage use the real Tough/Deadly Ascension thresholds.</summary>
public sealed class Wriggler : MonsterModel
{
    private bool _startStunned;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 18, 17);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 22, 21);

    private int BiteDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 7, 6);

    public bool StartStunned
    {
        get => _startStunned;
        set
        {
            AssertMutable();
            _startStunned = value;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var bite = new MoveState(
            "NASTY_BITE_MOVE",
            BiteMove,
            new SingleAttackIntent(BiteDamage));
        var wriggle = new MoveState(
            "WRIGGLE_MOVE",
            WriggleMove,
            new BuffIntent(),
            new StatusIntent(1));
        var spawned = new MoveState("SPAWNED_MOVE", _ => Task.CompletedTask, new StunIntent());
        var initial = new ConditionalBranchState("INIT_MOVE");
        initial.AddBranch(creature => creature.SlotName is "wriggler1" or "wriggler3", bite.Id);
        initial.AddBranch(creature => creature.SlotName is "wriggler2" or "wriggler4", wriggle.Id);
        bite.FollowUpState = wriggle;
        wriggle.FollowUpState = bite;
        spawned.FollowUpState = initial;
        MonsterState initialState = StartStunned ? spawned : initial;
        return new MonsterMoveStateMachine(
            new MonsterState[] { initial, bite, wriggle, spawned },
            initialState);
    }

    private async Task BiteMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(BiteDamage).FromMonster(this).Execute();
    }

    private async Task WriggleMove(IReadOnlyList<Creature> targets)
    {
        var combatState = Creature.CombatState
            ?? throw new InvalidOperationException("Wriggle requires active combat.");
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            var infection = (Infection)ModelDb.Card<Infection>().MutableClone();
            infection.AssignOwner(target.Player!);
            await CardPileCmd.Generate(combatState, infection, PileType.Discard, creator: null);
        }

        await PowerCmd.Apply<StrengthPower>(
            combatState,
            Creature,
            2m,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
