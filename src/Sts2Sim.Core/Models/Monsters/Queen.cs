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

    public override Task AfterDeath(Creature target, bool wasRemovalPrevented) => AfterDeath(target);

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 419, 400);

    public override int MaxInitialHp => MinInitialHp;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var puppetStrings = new MoveState("PUPPET_STRINGS_MOVE", PuppetStrings, new CardDebuffIntent());
        var youAreMine = new MoveState("YOU_ARE_MINE_MOVE", YouAreMine, new DebuffIntent());
        var bodyBranch = new ConditionalBranchState("AMALGAM_BRANCH")
            .AddBranch(_ => !_hasAmalgamDied, "BURN_BRIGHT_FOR_ME_MOVE")
            .AddBranch(_ => _hasAmalgamDied, "OFF_WITH_YOUR_HEAD_MOVE");
        var burnBright = new MoveState("BURN_BRIGHT_FOR_ME_MOVE", BurnBright, new BuffIntent(), new DefendIntent());
        var burnBrightBranch = new ConditionalBranchState("BURN_BRIGHT_FOR_ME_BRANCH")
            .AddBranch(_ => !_hasAmalgamDied, burnBright.Id)
            .AddBranch(_ => _hasAmalgamDied, "OFF_WITH_YOUR_HEAD_MOVE");
        var offWithYourHead = new MoveState(
            "OFF_WITH_YOUR_HEAD_MOVE", OffWithYourHead, new MultiAttackIntent(OffWithYourHeadDamage, 5));
        var execution = new MoveState("EXECUTION_MOVE", Execution, new SingleAttackIntent(ExecutionDamage));
        var enrage = new MoveState("ENRAGE_MOVE", Enrage, new BuffIntent());

        puppetStrings.FollowUpState = youAreMine;
        youAreMine.FollowUpState = bodyBranch;
        burnBright.FollowUpState = burnBrightBranch;
        offWithYourHead.FollowUpState = execution;
        execution.FollowUpState = enrage;
        enrage.FollowUpState = offWithYourHead;
        return new MonsterMoveStateMachine(
            [puppetStrings, youAreMine, bodyBranch, burnBright, burnBrightBranch, offWithYourHead, execution, enrage],
            puppetStrings);
    }

    public override Task AfterDeath(Creature target)
    {
        if (target.Monster is TorchHeadAmalgam && ReferenceEquals(target.CombatState, Creature.CombatState))
        {
            _hasAmalgamDied = true;
            if (NextMove?.StateId == "BURN_BRIGHT_FOR_ME_MOVE")
            {
                SetMoveImmediate(
                    AssertMoveState("ENRAGE_MOVE"),
                    forceTransition: true);
            }
        }

        return Task.CompletedTask;
    }

    private int OffWithYourHeadDamage => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);
    private int ExecutionDamage => Ascension(AscensionLevel.DeadlyEnemies, 18, 15);

    private async Task PuppetStrings(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            await PowerCmd.Apply<ChainsOfBindingPower>(Creature.CombatState!, target, 3m, Creature, null);
        }
    }

    private async Task YouAreMine(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 99m, Creature, null);
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 99m, Creature, null);
            await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 99m, Creature, null);
        }
    }

    private async Task BurnBright(IReadOnlyList<Creature> targets)
    {
        foreach (Creature ally in Creature.CombatState!.Enemies.Where(
                     ally => ally != Creature && !ally.IsDead))
        {
            await PowerCmd.Apply<StrengthPower>(Creature.CombatState, ally, 1m, Creature, null);
        }

        await CreatureCmd.GainBlock(Creature.CombatState, Creature, 20m, ValueProp.Move, null, null);
    }

    private Task OffWithYourHead(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(OffWithYourHeadDamage).WithHitCount(5).FromMonster(this).Execute();

    private Task Execution(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(ExecutionDamage).FromMonster(this).Execute();

    private Task Enrage(IReadOnlyList<Creature> targets) =>
        PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);

    private MoveState AssertMoveState(string stateId) =>
        (MoveState)(MoveStateMachine?.States[stateId]
            ?? throw new InvalidOperationException($"Move state {stateId} is not initialized."));

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_hasAmalgamDied);

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
