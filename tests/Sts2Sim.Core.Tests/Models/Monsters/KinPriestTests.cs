namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

[Collection("ModelDb")]
public sealed class KinPriestTests : IDisposable
{
    private readonly Task20BossTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 190)]
    [InlineData((int)AscensionLevel.ToughEnemies, 199)]
    public async Task HpAndOpening_UseAuthoritativeValues(int ascension, int expectedHp)
    {
        (KinPriest priest, _, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<KinPriest>(
                ascension,
                $"kin-priest-lifecycle-{ascension}");

        Assert.Equal(expectedHp, priest.MinInitialHp);
        Assert.Equal(expectedHp, priest.MaxInitialHp);
        Assert.Equal(expectedHp, priest.Creature.MaxHp);
        Assert.Equal("ORB_OF_FRAILTY_MOVE", priest.NextMove!.StateId);
    }

    [Theory]
    [InlineData(0, 8, 2)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 9, 3)]
    public async Task StateGraph_HasExactFrailtyWeaknessBeamRitualCycleAndIntents(
        int ascension,
        int expectedOrbDamage,
        int expectedRitualStrength)
    {
        (KinPriest priest, var room, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<KinPriest>(
                ascension,
                $"kin-priest-structure-{ascension}");
        MonsterMoveStateMachine machine = priest.MoveStateMachine!;
        MoveState frailty = Assert.IsType<MoveState>(machine.States["ORB_OF_FRAILTY_MOVE"]);
        MoveState weakness = Assert.IsType<MoveState>(machine.States["ORB_OF_WEAKNESS_MOVE"]);
        MoveState beam = Assert.IsType<MoveState>(machine.States["BEAM_MOVE"]);
        MoveState ritual = Assert.IsType<MoveState>(machine.States["RITUAL_MOVE"]);

        Assert.Equal(4, machine.States.Count);
        AssertOrbIntent(frailty, expectedOrbDamage, room, priest);
        AssertOrbIntent(weakness, expectedOrbDamage, room, priest);
        MultiAttackIntent beamIntent = Assert.IsType<MultiAttackIntent>(Assert.Single(beam.Intents));
        Assert.Equal(3, beamIntent.GetSingleDamage(room.Engine.State.Allies, priest.Creature));
        Assert.Equal(3, beamIntent.Repeats);
        Assert.IsType<BuffIntent>(Assert.Single(ritual.Intents));
        Assert.Same(weakness, frailty.FollowUpState);
        Assert.Same(beam, weakness.FollowUpState);
        Assert.Same(ritual, beam.FollowUpState);
        Assert.Same(frailty, ritual.FollowUpState);

        Task20BossTestFixture.ForceMove(priest, "RITUAL_MOVE");
        await priest.PerformMove();
        Assert.Equal(expectedRitualStrength, Assert.IsType<StrengthPower>(
            priest.Creature.GetPower<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task FrailtyWeaknessAndBeam_UseRealMultiplayerDamageAndPowers()
    {
        (KinPriest priest, _, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateSingleCombatAsync<KinPriest>(
                0,
                "kin-priest-multiplayer",
                playerCount: 2);
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();

        await priest.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(priest);
        Assert.Equal(hpBefore.Select(hp => hp - 8),
            players.Select(player => player.Creature.CurrentHp));
        Assert.All(players, player => Assert.Equal(1,
            Assert.IsType<FrailPower>(player.Creature.GetPower<FrailPower>()).Amount));

        await priest.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(priest);
        Assert.Equal(hpBefore.Select(hp => hp - 16),
            players.Select(player => player.Creature.CurrentHp));
        Assert.All(players, player => Assert.Equal(1,
            Assert.IsType<WeakPower>(player.Creature.GetPower<WeakPower>()).Amount));

        await priest.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(priest);
        Assert.Equal(hpBefore.Select(hp => hp - 25),
            players.Select(player => player.Creature.CurrentHp));
        Assert.Equal("RITUAL_MOVE", priest.NextMove!.StateId);
    }

    private static void AssertOrbIntent(
        MoveState state,
        int expectedDamage,
        Sts2Sim.Core.Rooms.CombatRoom room,
        KinPriest priest)
    {
        Assert.Collection(
            state.Intents,
            intent => Assert.Equal(expectedDamage,
                Assert.IsType<SingleAttackIntent>(intent)
                    .GetSingleDamage(room.Engine.State.Allies, priest.Creature)),
            intent => Assert.IsType<DebuffIntent>(intent));
    }
}
