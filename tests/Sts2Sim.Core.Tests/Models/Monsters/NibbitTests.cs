namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class NibbitTests : IDisposable
{
    private readonly Task17MonsterTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public void MutableClones_IsolateFrontAndAloneConfiguration()
    {
        var front = (Nibbit)ModelDb.Monster<Nibbit>().MutableClone();
        var back = (Nibbit)ModelDb.Monster<Nibbit>().MutableClone();
        var alone = (Nibbit)ModelDb.Monster<Nibbit>().MutableClone();

        front.IsFront = true;
        alone.IsAlone = true;

        Assert.True(front.IsFront);
        Assert.False(front.IsAlone);
        Assert.False(back.IsFront);
        Assert.False(back.IsAlone);
        Assert.False(alone.IsFront);
        Assert.True(alone.IsAlone);
        Assert.Equal(3, new[] { front, back, alone }
            .Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    [Theory]
    [InlineData(0, 42, 46)]
    [InlineData((int)AscensionLevel.ToughEnemies, 44, 48)]
    public async Task NormalAndWeakConfigurations_HaveExactHpSlotsFlagsAndOpenings(
        int ascension,
        int expectedMinHp,
        int expectedMaxHp)
    {
        (IReadOnlyList<Nibbit> normal, _, _) =
            await Task17MonsterTestFixture.CreateCombatAsync<Nibbit>(
                ascension,
                $"nibbits-normal-{ascension}",
                (nibbit => nibbit.IsFront = true, "front"),
                (null, "back"));
        (IReadOnlyList<Nibbit> weak, _, _) =
            await Task17MonsterTestFixture.CreateCombatAsync<Nibbit>(
                ascension,
                $"nibbits-weak-{ascension}",
                (nibbit => nibbit.IsAlone = true, null));

        Assert.Equal(2, normal.Count);
        Assert.NotSame(normal[0], normal[1]);
        Assert.True(normal[0].IsFront);
        Assert.False(normal[0].IsAlone);
        Assert.Equal("front", normal[0].Creature.SlotName);
        Assert.Equal("SLICE_MOVE", normal[0].NextMove!.StateId);
        Assert.False(normal[1].IsFront);
        Assert.False(normal[1].IsAlone);
        Assert.Equal("back", normal[1].Creature.SlotName);
        Assert.Equal("HISS_MOVE", normal[1].NextMove!.StateId);

        Nibbit alone = Assert.Single(weak);
        Assert.True(alone.IsAlone);
        Assert.False(alone.IsFront);
        Assert.Null(alone.Creature.SlotName);
        Assert.Equal("BUTT_MOVE", alone.NextMove!.StateId);

        Assert.All(normal.Concat(weak), nibbit =>
        {
            Assert.Equal(expectedMinHp, nibbit.MinInitialHp);
            Assert.Equal(expectedMaxHp, nibbit.MaxInitialHp);
            Assert.InRange(nibbit.Creature.MaxHp, expectedMinHp, expectedMaxHp);
            Assert.Equal(nibbit.Creature.MaxHp, nibbit.Creature.CurrentHp);
        });
    }

    [Theory]
    [InlineData(0, 12, 6)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 13, 7)]
    public void StateGraph_HasOrderedConditionalOpeningExactCycleAndIntents(
        int ascension,
        int expectedButtDamage,
        int expectedSliceDamage)
    {
        Nibbit nibbit = Task17MonsterTestFixture.CreateStructuralMonster<Nibbit>(
            ascension,
            $"nibbit-structure-{ascension}");
        MonsterMoveStateMachine machine = nibbit.MoveStateMachine!;
        ConditionalBranchState initial = Assert.IsType<ConditionalBranchState>(
            machine.States["INIT_MOVE"]);
        MoveState butt = Assert.IsType<MoveState>(machine.States["BUTT_MOVE"]);
        MoveState slice = Assert.IsType<MoveState>(machine.States["SLICE_MOVE"]);
        MoveState hiss = Assert.IsType<MoveState>(machine.States["HISS_MOVE"]);

        Assert.Equal(4, machine.States.Count);
        Assert.Equal(expectedButtDamage, SingleDamage(butt, nibbit));
        Assert.Collection(
            slice.Intents,
            intent => Assert.Equal(expectedSliceDamage,
                Assert.IsType<SingleAttackIntent>(intent)
                    .GetSingleDamage(PlayerTarget, nibbit.Creature)),
            intent => Assert.IsType<DefendIntent>(intent));
        Assert.IsType<BuffIntent>(Assert.Single(hiss.Intents));
        Assert.Same(slice, butt.FollowUpState);
        Assert.Same(hiss, slice.FollowUpState);
        Assert.Same(butt, hiss.FollowUpState);

        Assert.Equal("BUTT_MOVE", InitialStateFor(isFront: true, isAlone: true));
        Assert.Equal("HISS_MOVE", InitialStateFor(isFront: false, isAlone: false));
        Assert.Equal("SLICE_MOVE", InitialStateFor(isFront: true, isAlone: false));
        Assert.False(initial.ShouldAppearInLogs);

        // The native graph chooses its branch set when it is constructed.
        Nibbit configuredNormal = Task17MonsterTestFixture.CreateStructuralMonster<Nibbit>(
            0, "nibbit-branch-construction");
        configuredNormal.IsAlone = true;
        var normalInitial = Assert.IsType<ConditionalBranchState>(
            configuredNormal.MoveStateMachine!.States["INIT_MOVE"]);
        Assert.Equal("HISS_MOVE", normalInitial.GetNextState(configuredNormal.Creature,
            new Sts2Sim.Core.Random.Rng(0)));
    }

    [Theory]
    [InlineData(0, 12, 6, 5, 2)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 13, 7, 6, 3)]
    public async Task Moves_DealDamageGainBlockAndApplyStrengthThroughCombatCommands(
        int ascension,
        int expectedButtDamage,
        int expectedSliceDamage,
        int expectedSliceBlock,
        int expectedHissStrength)
    {
        (int buttLoss, Nibbit butt) = await PerformMove(ascension, "BUTT_MOVE");
        Assert.Equal(expectedButtDamage, buttLoss);
        Assert.Equal(0, butt.Creature.Block);

        (int sliceLoss, Nibbit slice) = await PerformMove(ascension, "SLICE_MOVE");
        Assert.Equal(expectedSliceDamage, sliceLoss);
        Assert.Equal(expectedSliceBlock, slice.Creature.Block);

        (int hissLoss, Nibbit hiss) = await PerformMove(ascension, "HISS_MOVE");
        Assert.Equal(0, hissLoss);
        Assert.Equal(expectedHissStrength,
            Assert.IsType<StrengthPower>(hiss.Creature.GetPower<StrengthPower>()).Amount);
    }

    [Theory]
    [InlineData(false, true, "BUTT_MOVE", "SLICE_MOVE", "HISS_MOVE", "BUTT_MOVE")]
    [InlineData(false, false, "HISS_MOVE", "BUTT_MOVE", "SLICE_MOVE", "HISS_MOVE")]
    [InlineData(true, false, "SLICE_MOVE", "HISS_MOVE", "BUTT_MOVE", "SLICE_MOVE")]
    public void FieldDrivenOpening_ContinuesThroughExactThreeMoveCycle(
        bool isFront,
        bool isAlone,
        params string[] expectedMoves)
    {
        Nibbit nibbit = Task17MonsterTestFixture.CreateStructuralMonster<Nibbit>(
            0,
            $"nibbit-cycle-{isFront}-{isAlone}",
            value =>
            {
                value.IsFront = isFront;
                value.IsAlone = isAlone;
            });

        foreach (string expectedMove in expectedMoves)
        {
            Assert.Equal(expectedMove, nibbit.NextMove!.StateId);
            nibbit.MoveStateMachine!.OnMovePerformed(nibbit.NextMove);
            nibbit.RollMove(nibbit.Creature.CombatState!.Allies);
        }
    }

    private static IReadOnlyList<Creature> PlayerTarget { get; } =
        new[] { Creature.CreateStandaloneForTests(100, 100) };

    private static int SingleDamage(MoveState move, Nibbit nibbit) =>
        Assert.IsType<SingleAttackIntent>(Assert.Single(move.Intents))
            .GetSingleDamage(PlayerTarget, nibbit.Creature);

    private static string InitialStateFor(bool isFront, bool isAlone)
    {
        Nibbit nibbit = Task17MonsterTestFixture.CreateStructuralMonster<Nibbit>(
            0,
            $"nibbit-initial-{isFront}-{isAlone}",
            value =>
            {
                value.IsFront = isFront;
                value.IsAlone = isAlone;
            });
        return nibbit.NextMove!.StateId;
    }

    private static async Task<(int HpLoss, Nibbit Nibbit)> PerformMove(
        int ascension,
        string stateId)
    {
        (IReadOnlyList<Nibbit> nibbits, _, var player) =
            await Task17MonsterTestFixture.CreateCombatAsync<Nibbit>(
                ascension,
                $"nibbit-effect-{stateId}-{ascension}",
                (null, null));
        Nibbit nibbit = Assert.Single(nibbits);
        Task17MonsterTestFixture.ForceMove(nibbit, stateId);
        int hpBefore = player.Creature.CurrentHp;

        await nibbit.PerformMove();

        return (hpBefore - player.Creature.CurrentHp, nibbit);
    }
}
