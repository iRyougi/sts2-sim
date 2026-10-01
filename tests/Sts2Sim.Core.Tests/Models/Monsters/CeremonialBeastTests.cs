namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Models.Afflictions;

[Collection("ModelDb")]
public sealed class CeremonialBeastTests : IDisposable
{
    private readonly Task20BossTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 252)]
    [InlineData((int)AscensionLevel.ToughEnemies, 262)]
    public async Task HpOpeningAndPhaseFlags_UseAuthoritativeValues(int ascension, int expectedHp)
    {
        (CeremonialBeast beast, _, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<CeremonialBeast>(
                ascension,
                $"ceremonial-lifecycle-{ascension}");

        Assert.Equal(expectedHp, beast.MinInitialHp);
        Assert.Equal(expectedHp, beast.MaxInitialHp);
        Assert.Equal(expectedHp, beast.Creature.MaxHp);
        Assert.Equal("STAMP_MOVE", beast.NextMove!.StateId);
        Assert.False(beast.IsStunnedByPlowRemoval);
        Assert.False(beast.IsInSecondPhase);
    }

    [Theory]
    [InlineData(0, 18, 15, 17)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 20, 17, 19)]
    public async Task StateGraph_HasExactTwoPhaseTopologyAndIntents(
        int ascension,
        int expectedPlow,
        int expectedStomp,
        int expectedCrush)
    {
        (CeremonialBeast beast, var room, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<CeremonialBeast>(
                ascension,
                $"ceremonial-structure-{ascension}");
        MonsterMoveStateMachine machine = beast.MoveStateMachine!;
        MoveState stamp = Assert.IsType<MoveState>(machine.States["STAMP_MOVE"]);
        MoveState plow = Assert.IsType<MoveState>(machine.States["PLOW_MOVE"]);
        MoveState stun = Assert.IsType<MoveState>(machine.States["STUN_MOVE"]);
        MoveState cry = Assert.IsType<MoveState>(machine.States["BEAST_CRY_MOVE"]);
        MoveState stomp = Assert.IsType<MoveState>(machine.States["STOMP_MOVE"]);
        MoveState crush = Assert.IsType<MoveState>(machine.States["CRUSH_MOVE"]);

        Assert.Equal(6, machine.States.Count);
        Assert.IsType<BuffIntent>(Assert.Single(stamp.Intents));
        Assert.Collection(
            plow.Intents,
            intent => Assert.Equal(expectedPlow,
                Assert.IsType<SingleAttackIntent>(intent)
                    .GetSingleDamage(room.Engine.State.Allies, beast.Creature)),
            intent => Assert.IsType<BuffIntent>(intent));
        Assert.IsType<StunIntent>(Assert.Single(stun.Intents));
        Assert.True(stun.MustPerformOnceBeforeTransitioning);
        Assert.IsType<DebuffIntent>(Assert.Single(cry.Intents));
        Assert.Equal(expectedStomp, Assert.IsType<SingleAttackIntent>(
            Assert.Single(stomp.Intents)).GetSingleDamage(
                room.Engine.State.Allies,
                beast.Creature));
        Assert.Collection(
            crush.Intents,
            intent => Assert.Equal(expectedCrush,
                Assert.IsType<SingleAttackIntent>(intent)
                    .GetSingleDamage(room.Engine.State.Allies, beast.Creature)),
            intent => Assert.IsType<BuffIntent>(intent));
        Assert.Same(plow, stamp.FollowUpState);
        Assert.Same(plow, plow.FollowUpState);
        Assert.Same(cry, stun.FollowUpState);
        Assert.Same(stomp, cry.FollowUpState);
        Assert.Same(crush, stomp.FollowUpState);
        Assert.Same(cry, crush.FollowUpState);
        Assert.DoesNotContain(stun,
            new[] { stamp.FollowUpState, plow.FollowUpState, cry.FollowUpState,
                stomp.FollowUpState, crush.FollowUpState });
    }

    [Theory]
    [InlineData(0, 150)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 160)]
    public async Task Stamp_AppliesAuthoritativePlowThreshold(int ascension, int expectedAmount)
    {
        (CeremonialBeast beast, _, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<CeremonialBeast>(
                ascension,
                $"ceremonial-stamp-{ascension}");

        await beast.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(beast);

        Assert.Equal(expectedAmount, Assert.IsType<PlowPower>(
            beast.Creature.GetPower<PlowPower>()).Amount);
        Assert.Equal("PLOW_MOVE", beast.NextMove!.StateId);
    }

    [Theory]
    [InlineData(0, 18)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 20)]
    public async Task Plow_SelfLoopsWithRealMultiplayerDamageAndStrength(
        int ascension,
        int expectedDamage)
    {
        (CeremonialBeast beast, _, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateSingleCombatAsync<CeremonialBeast>(
                ascension,
                $"ceremonial-plow-{ascension}",
                playerCount: 2);
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();
        Task20BossTestFixture.ForceMove(beast, "PLOW_MOVE");

        await beast.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(beast);

        Assert.Equal(hpBefore.Select(hp => hp - expectedDamage),
            players.Select(player => player.Creature.CurrentHp));
        Assert.Equal(2, Assert.IsType<StrengthPower>(
            beast.Creature.GetPower<StrengthPower>()).Amount);
        Assert.Equal("PLOW_MOVE", beast.NextMove!.StateId);
    }

    [Fact]
    public async Task ProductionPhaseBreak_ForcesOwnStunOnceThenCompletesSecondPhaseAndWins()
    {
        (CeremonialBeast beast, var room, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateSingleCombatAsync<CeremonialBeast>(
                0,
                "ceremonial-production-phase",
                playerCount: 2);
        Player dealer = players[0];

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal("PLOW_MOVE", beast.NextMove!.StateId);
        Assert.Equal(150, Assert.IsType<PlowPower>(
            beast.Creature.GetPower<PlowPower>()).Amount);

        await DamageBoss(room, beast, dealer, 101m);

        Assert.Equal(151, beast.Creature.CurrentHp);
        Assert.NotNull(beast.Creature.GetPower<PlowPower>());
        Assert.False(beast.IsInSecondPhase);
        Assert.Equal("PLOW_MOVE", beast.NextMove!.StateId);

        await PowerCmd.Apply<StrengthPower>(
            room.Engine.State, beast.Creature, 5m, beast.Creature, cardSource: null);
        await PowerCmd.Apply<TemporaryStrengthPower>(
            room.Engine.State, beast.Creature, 3m, beast.Creature, cardSource: null);
        await DamageBoss(room, beast, dealer, 1m);

        Assert.Equal(150, beast.Creature.CurrentHp);
        Assert.Null(beast.Creature.GetPower<PlowPower>());
        Assert.Null(beast.Creature.GetPower<StrengthPower>());
        Assert.Null(beast.Creature.GetPower<TemporaryStrengthPower>());
        Assert.True(beast.IsStunnedByPlowRemoval);
        Assert.True(beast.IsInSecondPhase);
        MoveState ownStun = Assert.IsType<MoveState>(
            beast.MoveStateMachine!.States["STUN_MOVE"]);
        Assert.Same(ownStun, beast.NextMove);
        Assert.True(beast.NextMove!.MustPerformOnceBeforeTransitioning);

        int[] hpBeforeStun = players.Select(player => player.Creature.CurrentHp).ToArray();
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(hpBeforeStun, players.Select(player => player.Creature.CurrentHp));
        Assert.All(players, player => Assert.Null(player.Creature.GetPower<RingingPower>()));
        Assert.False(beast.IsStunnedByPlowRemoval);
        Assert.True(beast.IsInSecondPhase);
        Assert.Equal("BEAST_CRY_MOVE", beast.NextMove!.StateId);

        await room.Engine.EndPlayerTurnAsync();

        foreach (Player player in players)
        {
            RingingPower ringing = Assert.IsType<RingingPower>(
                player.Creature.GetPower<RingingPower>());
            Assert.Equal(1, ringing.Amount);
            Assert.All(player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards),
                card => Assert.True(card.Affliction is Ringing));
        }
        Assert.Equal("STOMP_MOVE", beast.NextMove!.StateId);

        int[] hpBeforeStomp = players.Select(player => player.Creature.CurrentHp).ToArray();
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(hpBeforeStomp.Select(hp => hp - 15),
            players.Select(player => player.Creature.CurrentHp));
        Assert.All(players, player => Assert.Null(player.Creature.GetPower<RingingPower>()));
        Assert.Equal("CRUSH_MOVE", beast.NextMove!.StateId);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(hpBeforeStomp.Select(hp => hp - 32),
            players.Select(player => player.Creature.CurrentHp));
        Assert.Equal(3, Assert.IsType<StrengthPower>(
            beast.Creature.GetPower<StrengthPower>()).Amount);
        Assert.Equal("BEAST_CRY_MOVE", beast.NextMove!.StateId);
        Assert.DoesNotContain(
            beast.MoveStateMachine.StateLog.SkipWhile(state => state.Id != "STUN_MOVE").Skip(1),
            state => state.Id is "STAMP_MOVE" or "PLOW_MOVE" or "STUN_MOVE");

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal("STOMP_MOVE", beast.NextMove!.StateId);
        Assert.All(players, player => Assert.NotNull(player.Creature.GetPower<RingingPower>()));

        DamageResult killed = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { beast.Creature },
            beast.Creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: dealer.Creature,
            cardSource: null,
            cardPlay: null));

        Assert.True(killed.WasTargetKilled);
        Assert.True(room.Engine.CheckWinCondition());
        Assert.True(room.Engine.Won);
        Assert.False(room.Engine.IsInProgress);
    }

    [Fact]
    public async Task LethalThresholdHit_DoesNotForceDeadBossIntoSecondPhase()
    {
        (CeremonialBeast beast, var room, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateSingleCombatAsync<CeremonialBeast>(
                0,
                "ceremonial-lethal-threshold");
        Player dealer = Assert.Single(players);
        await beast.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(beast);

        DamageResult killed = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { beast.Creature },
            beast.Creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: dealer.Creature,
            cardSource: null,
            cardPlay: null));

        Assert.True(killed.WasTargetKilled);
        Assert.True(beast.Creature.IsDead);
        Assert.False(beast.IsStunnedByPlowRemoval);
        Assert.False(beast.IsInSecondPhase);
        Assert.NotEqual("STUN_MOVE", beast.NextMove!.StateId);
    }

    private static async Task DamageBoss(
        Sts2Sim.Core.Rooms.CombatRoom room,
        CeremonialBeast beast,
        Player dealer,
        decimal amount)
    {
        DamageResult result = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { beast.Creature },
            amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: dealer.Creature,
            cardSource: null,
            cardPlay: null));
        Assert.Equal(amount, result.UnblockedDamage);
    }
}
