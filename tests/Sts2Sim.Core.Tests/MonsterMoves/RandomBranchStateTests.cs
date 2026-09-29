namespace Sts2Sim.Core.Tests.MonsterMoves;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Random;

[Collection("ModelDb")]
public class RandomBranchStateTests
{
    public RandomBranchStateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Wriggler) });
    }

    [Fact]
    public void GetNextState_WeightedBranches_FollowsConfiguredWeights()
    {
        (Creature owner, MoveState bite, MoveState wriggle) = CreateOwner();
        var subject = new RandomBranchState("RAND");
        subject.AddBranch(bite, MoveRepeatType.CanRepeatForever, 40f);
        subject.AddBranch(wriggle, MoveRepeatType.CanRepeatForever, 60f);
        var rng = new Rng(20260803uL);
        int biteCount = 0;

        for (int i = 0; i < 1000; i++)
        {
            if (subject.GetNextState(owner, rng) == bite.Id)
            {
                biteCount++;
            }
        }

        Assert.InRange(biteCount, 350, 450);
    }

    [Fact]
    public void GetNextState_CannotRepeat_ExcludesTheMostRecentMatchingMove()
    {
        (Creature owner, MoveState bite, MoveState wriggle) = CreateOwner();
        SetHistory(owner, bite);
        var subject = new RandomBranchState("RAND");
        subject.AddBranch(bite, MoveRepeatType.CannotRepeat);
        subject.AddBranch(wriggle, MoveRepeatType.CanRepeatForever);

        string nextState = subject.GetNextState(owner, new Rng(1uL));

        Assert.Equal(wriggle.Id, nextState);
    }

    [Fact]
    public void GetNextState_UseOnlyOnce_ExcludesAStateThatAlreadyAppearedInTheLog()
    {
        (Creature owner, MoveState bite, MoveState wriggle) = CreateOwner();
        SetHistory(owner, bite);
        var subject = new RandomBranchState("RAND");
        subject.AddBranch(bite, MoveRepeatType.UseOnlyOnce);
        subject.AddBranch(wriggle, MoveRepeatType.CanRepeatForever);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(wriggle.Id, subject.GetNextState(owner, new Rng((ulong)i)));
        }
    }

    [Fact]
    public void GetNextState_CanRepeatXTimes_ExcludesAStateAfterItsMaximumConsecutiveRepeats()
    {
        (Creature owner, MoveState bite, MoveState wriggle) = CreateOwner();
        SetHistory(owner, bite, bite);
        var subject = new RandomBranchState("RAND");
        subject.AddBranch(bite, maxRepeats: 2);
        subject.AddBranch(wriggle, MoveRepeatType.CanRepeatForever);

        string nextState = subject.GetNextState(owner, new Rng(1uL));

        Assert.Equal(wriggle.Id, nextState);
    }

    [Fact]
    public void GetNextState_CanRepeatForever_RemainsEligibleAfterRepeatedSelections()
    {
        (Creature owner, MoveState bite, _) = CreateOwner();
        SetHistory(owner, bite, bite, bite, bite);
        var subject = new RandomBranchState("RAND");
        subject.AddBranch(bite, MoveRepeatType.CanRepeatForever);

        Assert.Equal(bite.Id, subject.GetNextState(owner, new Rng(1uL)));
    }

    [Fact]
    public void GetNextState_Cooldown_ExcludesAMoveFoundInTheRecentMoveHistory()
    {
        (Creature owner, MoveState bite, MoveState wriggle) = CreateOwner();
        SetHistory(owner, bite, wriggle);
        var subject = new RandomBranchState("RAND");
        subject.AddBranch(bite, cooldown: 2, MoveRepeatType.CanRepeatForever);
        subject.AddBranch(wriggle, MoveRepeatType.CanRepeatForever);

        string nextState = subject.GetNextState(owner, new Rng(1uL));

        Assert.Equal(wriggle.Id, nextState);
    }

    [Fact]
    public void GetNextState_AllBranchesExcluded_FallsBackToTheFirstBranch()
    {
        (Creature owner, MoveState bite, MoveState wriggle) = CreateOwner();
        SetHistory(owner, bite, wriggle);
        var subject = new RandomBranchState("RAND");
        subject.AddBranch(bite, MoveRepeatType.UseOnlyOnce);
        subject.AddBranch(wriggle, MoveRepeatType.UseOnlyOnce);

        Assert.Equal(bite.Id, subject.GetNextState(owner, new Rng(1uL)));
    }

    [Fact]
    public void GetNextState_NoBranches_ThrowsNoValidStateError()
    {
        (Creature owner, _, _) = CreateOwner();
        var subject = new RandomBranchState("RAND");

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => subject.GetNextState(owner, new Rng(1uL)));

        Assert.Equal("No valid state found in RandomBranchState RAND!", error.Message);
    }

    private static (Creature Owner, MoveState Bite, MoveState Wriggle) CreateOwner()
    {
        var monster = (Wriggler)ModelDb.Monster<Wriggler>().MutableClone();
        var owner = new Creature(monster, CombatSide.Enemy);
        monster.AssignCreature(owner);
        monster.SetUpForCombat();
        MonsterMoveStateMachine machine = monster.MoveStateMachine!;
        return (owner, (MoveState)machine.States["NASTY_BITE_MOVE"], (MoveState)machine.States["WRIGGLE_MOVE"]);
    }

    private static void SetHistory(Creature owner, params MonsterState[] states)
    {
        List<MonsterState> stateLog = owner.Monster!.MoveStateMachine!.StateLog;
        stateLog.Clear();
        stateLog.AddRange(states);
    }
}
