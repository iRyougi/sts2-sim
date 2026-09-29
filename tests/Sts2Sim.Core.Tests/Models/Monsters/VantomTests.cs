namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class VantomTests : IDisposable
{
    private readonly Task20BossTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 173, 8)]
    [InlineData((int)AscensionLevel.ToughEnemies, 183, 9)]
    public async Task HpOpeningAndSlipperyLifecycle_UseAuthoritativeValues(
        int ascension,
        int expectedHp,
        int expectedSlippery)
    {
        (Vantom vantom, _, _) = await Task20BossTestFixture.CreateSingleCombatAsync<Vantom>(
            ascension,
            $"vantom-lifecycle-{ascension}");

        Assert.Equal(expectedHp, vantom.MinInitialHp);
        Assert.Equal(expectedHp, vantom.MaxInitialHp);
        Assert.Equal(expectedHp, vantom.Creature.MaxHp);
        Assert.Equal("INK_BLOT_MOVE", vantom.NextMove!.StateId);
        Assert.Equal(expectedSlippery,
            Assert.IsType<SlipperyPower>(vantom.Creature.GetPower<SlipperyPower>()).Amount);
    }

    [Theory]
    [InlineData(0, 7, 6, 26)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 8, 7, 30)]
    public async Task StateGraph_HasExactFourMoveCycleAndIntents(
        int ascension,
        int expectedInkBlot,
        int expectedLance,
        int expectedDismember)
    {
        (Vantom vantom, var room, _) =
            await Task20BossTestFixture.CreateSingleCombatAsync<Vantom>(
                ascension,
                $"vantom-structure-{ascension}");
        MonsterMoveStateMachine machine = vantom.MoveStateMachine!;
        MoveState inkBlot = Assert.IsType<MoveState>(machine.States["INK_BLOT_MOVE"]);
        MoveState lance = Assert.IsType<MoveState>(machine.States["INKY_LANCE_MOVE"]);
        MoveState dismember = Assert.IsType<MoveState>(machine.States["DISMEMBER_MOVE"]);
        MoveState prepare = Assert.IsType<MoveState>(machine.States["PREPARE_MOVE"]);

        Assert.Equal(4, machine.States.Count);
        Assert.Equal(expectedInkBlot, Assert.IsType<SingleAttackIntent>(
            Assert.Single(inkBlot.Intents)).GetSingleDamage(
                room.Engine.State.Allies,
                vantom.Creature));
        MultiAttackIntent lanceIntent = Assert.IsType<MultiAttackIntent>(
            Assert.Single(lance.Intents));
        Assert.Equal(expectedLance,
            lanceIntent.GetSingleDamage(room.Engine.State.Allies, vantom.Creature));
        Assert.Equal(2, lanceIntent.Repeats);
        Assert.Collection(
            dismember.Intents,
            intent => Assert.Equal(expectedDismember,
                Assert.IsType<SingleAttackIntent>(intent)
                    .GetSingleDamage(room.Engine.State.Allies, vantom.Creature)),
            intent => Assert.Equal(3, Assert.IsType<StatusIntent>(intent).Count));
        Assert.IsType<BuffIntent>(Assert.Single(prepare.Intents));
        Assert.Same(lance, inkBlot.FollowUpState);
        Assert.Same(dismember, lance.FollowUpState);
        Assert.Same(prepare, dismember.FollowUpState);
        Assert.Same(inkBlot, prepare.FollowUpState);

        Assert.Equal("INK_BLOT_MOVE", vantom.NextMove!.StateId);
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal("INKY_LANCE_MOVE", vantom.NextMove!.StateId);
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal("DISMEMBER_MOVE", vantom.NextMove!.StateId);
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal("PREPARE_MOVE", vantom.NextMove!.StateId);
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal("INK_BLOT_MOVE", vantom.NextMove!.StateId);
    }

    [Theory]
    [InlineData(30, 4, 3)]
    [InlineData(1, 0, 0)]
    public async Task Dismember_GeneratesWoundsOnlyForLivingPlayer(
        int initialHp,
        int expectedHp,
        int expectedWounds)
    {
        (Vantom vantom, _, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateSingleCombatAsync<Vantom>(
                0,
                $"vantom-dismember-{initialHp}");
        Player player = Assert.Single(players);
        player.Creature.SetCurrentHpInternal(initialHp);
        Task20BossTestFixture.ForceMove(vantom, "DISMEMBER_MOVE");

        await vantom.PerformMove();

        Assert.Equal(expectedHp, player.Creature.CurrentHp);
        Assert.Equal(expectedWounds,
            player.PlayerCombatState!.DiscardPile.Cards.OfType<Wound>().Count());
    }

    [Fact]
    public async Task FullCycle_UsesRealMultiplayerEffectsAndBossDeathEndsCombat()
    {
        (Vantom vantom, var room, IReadOnlyList<Player> players) =
            await Task20BossTestFixture.CreateSingleCombatAsync<Vantom>(
                0,
                "vantom-full-cycle",
                playerCount: 2);
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();

        await vantom.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal(hpBefore.Select(hp => hp - 7),
            players.Select(player => player.Creature.CurrentHp));

        await vantom.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal(hpBefore.Select(hp => hp - 19),
            players.Select(player => player.Creature.CurrentHp));

        await vantom.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal(hpBefore.Select(hp => hp - 45),
            players.Select(player => player.Creature.CurrentHp));
        foreach (Player player in players)
        {
            Wound[] wounds = player.PlayerCombatState!.DiscardPile.Cards.OfType<Wound>().ToArray();
            Assert.Equal(3, wounds.Length);
            Assert.Equal(3, wounds.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.All(wounds, wound =>
            {
                Assert.Same(player, wound.Owner);
                Assert.Same(player.PlayerCombatState.DiscardPile, wound.Pile);
                Assert.Same(room.Engine.State, wound.CombatState);
            });
        }

        await vantom.PerformMove();
        Task20BossTestFixture.RollAfterPerformed(vantom);
        Assert.Equal(2, Assert.IsType<StrengthPower>(
            vantom.Creature.GetPower<StrengthPower>()).Amount);
        Assert.Equal("INK_BLOT_MOVE", vantom.NextMove!.StateId);

        await PowerCmd.Remove(Assert.IsType<SlipperyPower>(
            vantom.Creature.GetPower<SlipperyPower>()));
        DamageResult killed = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { vantom.Creature },
            vantom.Creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: players[0].Creature,
            cardSource: null,
            cardPlay: null));

        Assert.True(killed.WasTargetKilled);
        Assert.True(room.Engine.CheckWinCondition());
        Assert.True(room.Engine.Won);
        Assert.False(room.Engine.IsInProgress);
    }
}
