namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class Queen : MonsterModel
{
    private bool _hasAmalgamDied;
    private Creature? _amalgam;
    private MoveState _burnBrightForMeState = null!;
    private MoveState _enragedState = null!;

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 419, 400);

    public override int MaxInitialHp => MinInitialHp;

    private bool HasAmalgamDied
    {
        get => _hasAmalgamDied;
        set
        {
            AssertMutable();
            _hasAmalgamDied = value;
        }
    }

    private Creature? Amalgam
    {
        get => _amalgam;
        set
        {
            AssertMutable();
            _amalgam = value;
        }
    }

    private MoveState BurnBrightForMeState
    {
        get => _burnBrightForMeState;
        set
        {
            AssertMutable();
            _burnBrightForMeState = value;
        }
    }

    private MoveState EnragedState
    {
        get => _enragedState;
        set
        {
            AssertMutable();
            _enragedState = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        Amalgam = Creature.CombatState!.Enemies.First(creature => creature.Monster is TorchHeadAmalgam);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var states = new List<MonsterState>();
        var puppetStrings = new MoveState("PUPPET_STRINGS_MOVE", PuppetStringsMove, new CardDebuffIntent());
        var youAreMine = new MoveState("YOU_ARE_MINE_MOVE", YoureMineMove, new DebuffIntent());
        var bodyBranch = new ConditionalBranchState("YOURE_MINE_NOW_BRANCH");
        BurnBrightForMeState = new MoveState(
            "BURN_BRIGHT_FOR_ME_MOVE", BurnBrightForMeMove, new BuffIntent(), new DefendIntent());
        var burnBrightBranch = new ConditionalBranchState("BURN_BRIGHT_FOR_ME_BRANCH");
        var offWithYourHead = new MoveState(
            "OFF_WITH_YOUR_HEAD_MOVE", OffWithYourHeadMove, new MultiAttackIntent(OffWithYourHeadDamage, 5));
        var execution = new MoveState("EXECUTION_MOVE", ExecutionMove, new SingleAttackIntent(ExecutionDamage));
        EnragedState = new MoveState("ENRAGE_MOVE", EnrageMove, new BuffIntent());

        puppetStrings.FollowUpState = youAreMine;
        youAreMine.FollowUpState = bodyBranch;
        bodyBranch.AddBranch(_ => !HasAmalgamDied, BurnBrightForMeState.Id);
        bodyBranch.AddBranch(_ => HasAmalgamDied, offWithYourHead.Id);
        BurnBrightForMeState.FollowUpState = burnBrightBranch;
        burnBrightBranch.AddBranch(_ => !HasAmalgamDied, BurnBrightForMeState.Id);
        burnBrightBranch.AddBranch(_ => HasAmalgamDied, offWithYourHead.Id);
        offWithYourHead.FollowUpState = execution;
        execution.FollowUpState = EnragedState;
        EnragedState.FollowUpState = offWithYourHead;
        states.Add(puppetStrings);
        states.Add(youAreMine);
        states.Add(BurnBrightForMeState);
        states.Add(burnBrightBranch);
        states.Add(bodyBranch);
        states.Add(offWithYourHead);
        states.Add(execution);
        states.Add(EnragedState);
        return new MonsterMoveStateMachine(states, puppetStrings);
    }

    public override Task AfterDeath(Creature target, bool wasRemovalPrevented)
    {
        if (target.Monster is TorchHeadAmalgam && Creature.IsAlive)
        {
            HasAmalgamDied = true;
            Amalgam = null;
            if (NextMove == BurnBrightForMeState)
            {
                SetMoveImmediate(EnragedState);
            }
        }

        return Task.CompletedTask;
    }

    private int OffWithYourHeadDamage => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);
    private int ExecutionDamage => Ascension(AscensionLevel.DeadlyEnemies, 18, 15);

    private async Task PuppetStringsMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<ChainsOfBindingPower>(Creature.CombatState!, target, 3m, Creature, null);
        }
    }

    private async Task YoureMineMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 99m, Creature, null);
        }
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 99m, Creature, null);
        }
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 99m, Creature, null);
        }
    }

    private async Task BurnBrightForMeMove(IReadOnlyList<Creature> targets)
    {
        int strengthAmount = Ascension(AscensionLevel.DeadlyEnemies, 1, 1);
        List<Creature> teammates = Creature.CombatState!.Enemies.ToList();
        foreach (Creature ally in teammates.Where(ally => ally != Creature))
        {
            await PowerCmd.Apply<StrengthPower>(Creature.CombatState, ally, strengthAmount, Creature, null);
        }

        await CreatureCmd.GainBlock(Creature.CombatState, Creature, 20m, ValueProp.Move, null, null);
    }

    private async Task OffWithYourHeadMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(OffWithYourHeadDamage).WithHitCount(5).FromMonster(this).Execute();

    private async Task ExecutionMove(IReadOnlyList<Creature> targets) =>
        await DamageCmd.Attack(ExecutionDamage).FromMonster(this).Execute();

    private async Task EnrageMove(IReadOnlyList<Creature> targets) =>
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);

    internal override void RestoreCombatCloneReferencesFrom(
        MonsterModel source,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        Amalgam = ((Queen)source).Amalgam is { } amalgam ? creatureMap[amalgam] : null;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(HasAmalgamDied);
        context.AppendCreatureReferences(ref builder, Amalgam is { } amalgam ? [amalgam] : []);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
