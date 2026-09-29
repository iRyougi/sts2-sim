namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class BowlbugRock : MonsterModel
{
    private bool _isOffBalance;

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 46, 45);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 49, 48);
    private int Damage => Ascension(AscensionLevel.DeadlyEnemies, 16, 15);

    public bool IsOffBalance
    {
        get => _isOffBalance;
        set
        {
            AssertMutable();
            _isOffBalance = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<ImbalancedPower>(Creature.CombatState!, Creature, 1m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var headbutt = new MoveState("HEADBUTT_MOVE", HeadbuttMove, new SingleAttackIntent(Damage));
        var dizzy = new MoveState("DIZZY_MOVE", DizzyMove, new StunIntent());
        var branch = new ConditionalBranchState("POST_HEADBUTT")
            .AddBranch(_ => IsOffBalance, dizzy.Id)
            .AddBranch(_ => !IsOffBalance, headbutt.Id);
        headbutt.FollowUpState = branch;
        dizzy.FollowUpState = headbutt;
        return new MonsterMoveStateMachine(new MonsterState[] { headbutt, dizzy, branch }, headbutt);
    }

    private async Task HeadbuttMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(Damage).FromMonster(this).Execute();
        if (IsOffBalance)
        {
            await Stun();
        }
    }

    private Task Stun() => CreatureCmd.Stun<BowlbugRock>(
        Creature,
        static (rock, targets) => rock.DizzyMove(targets));

    private Task DizzyMove(IReadOnlyList<Creature> targets)
    {
        IsOffBalance = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_isOffBalance);

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
