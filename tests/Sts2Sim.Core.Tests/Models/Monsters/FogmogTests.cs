namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class FogmogTests : IDisposable
{
    private readonly Task16MonsterTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task DeclaredEncounterSlots_OrderNaturalIllusionSpawnAndClonedFutureSpawn()
    {
        // XWDT4877APQF F14 and FogmogNormal.Slots: illusion precedes fogmog.
        var act = new Overgrowth();
        var pool = (IReadOnlyList<Sts2Sim.Core.Content.EncounterDefinition>)typeof(Overgrowth)
            .GetProperty("MonsterEncounters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(act)!;
        var encounter = pool.Single(e => e.Name == "Fogmog");
        var run = new RunState("XWDT4877APQF", act, 10);
        run.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Silent>(), run));
        var room = new Sts2Sim.Core.Rooms.CombatRoom(() => (encounter, encounter.CreateMonsters()), Sts2Sim.Core.Rooms.RoomType.Monster);
        run.PushRoom(room);
        await room.Enter(run);
        CombatState clone = room.Engine.State.Clone();
        uint? fogmogId = Assert.Single(room.Engine.State.Enemies).CombatId;

        await room.Engine.EndPlayerTurnAsync();
        await clone.Engine!.EndPlayerTurnAsync();

        foreach (CombatState state in new[] { room.Engine.State, clone })
        {
            Assert.Equal(new[] { "EYE_WITH_TEETH", "FOGMOG" }, state.Enemies.Select(c => c.Monster!.Id.Entry));
            Assert.Equal(new[] { "illusion", "fogmog" }, state.Enemies.Select(c => c.SlotName));
            Assert.Equal(fogmogId, state.Enemies[1].CombatId);
            Assert.Equal(new[] { "FOGMOG", "EYE_WITH_TEETH" }, state.SpawnedEnemies.Select(c => c.Monster!.Id.Entry));
        }
        Assert.Equal(room.Engine.State.Enemies.Select(c => c.CombatId), clone.Enemies.Select(c => c.CombatId));
        Assert.Equal(room.Engine.State.RunState.Rng.CombatTargets.Counter, clone.RunState.Rng.CombatTargets.Counter);
    }

    [Theory]
    [InlineData(0, 74)]
    [InlineData((int)AscensionLevel.ToughEnemies, 78)]
    public async Task FixedHpAndInitialMove_UseAuthoritativeValues(int ascension, int expectedHp)
    {
        (Fogmog fogmog, _, _) = await Task16MonsterTestFixture.CreateCombatAsync<Fogmog>(
            ascension,
            $"fogmog-hp-{ascension}",
            slotName: "fogmog");

        Assert.Equal(expectedHp, fogmog.MinInitialHp);
        Assert.Equal(expectedHp, fogmog.MaxInitialHp);
        Assert.Equal(expectedHp, fogmog.Creature.MaxHp);
        Assert.Equal("fogmog", fogmog.Creature.SlotName);
        Assert.Equal("ILLUSION_MOVE", fogmog.NextMove!.StateId);
    }

    [Theory]
    [InlineData(0, 8, 14)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 9, 16)]
    public void StateGraph_IntentsWeightsAndRuntimeBranchesMatchAuthoritativeSource(
        int ascension,
        int expectedSwipeDamage,
        int expectedHeadbuttDamage)
    {
        Fogmog inspected = CreateStructuralFogmog(ascension, "fogmog-structure");
        MonsterMoveStateMachine machine = inspected.MoveStateMachine!;
        MoveState illusion = Assert.IsType<MoveState>(machine.States["ILLUSION_MOVE"]);
        MoveState swipe = Assert.IsType<MoveState>(machine.States["SWIPE_MOVE"]);
        MoveState swipeRandom = Assert.IsType<MoveState>(machine.States["SWIPE_RANDOM_MOVE"]);
        MoveState headbutt = Assert.IsType<MoveState>(machine.States["HEADBUTT_MOVE"]);
        RandomBranchState branch = Assert.IsType<RandomBranchState>(machine.States["BRANCH"]);

        Assert.IsType<SummonIntent>(Assert.Single(illusion.Intents));
        Assert.Collection(
            swipe.Intents,
            intent => Assert.Equal(
                expectedSwipeDamage,
                Assert.IsType<SingleAttackIntent>(intent).GetSingleDamage(
                    new[] { Creature.CreateStandaloneForTests(100, 100) },
                    inspected.Creature)),
            intent => Assert.IsType<BuffIntent>(intent));
        Assert.Collection(
            swipeRandom.Intents,
            intent => Assert.Equal(
                expectedSwipeDamage,
                Assert.IsType<SingleAttackIntent>(intent).GetSingleDamage(
                    new[] { Creature.CreateStandaloneForTests(100, 100) },
                    inspected.Creature)),
            intent => Assert.IsType<BuffIntent>(intent));
        Assert.Equal(
            expectedHeadbuttDamage,
            Assert.IsType<SingleAttackIntent>(Assert.Single(headbutt.Intents))
                .GetSingleDamage(
                    new[] { Creature.CreateStandaloneForTests(100, 100) },
                    inspected.Creature));
        Assert.Same(swipe, illusion.FollowUpState);
        Assert.Same(branch, swipe.FollowUpState);
        Assert.Same(headbutt, swipeRandom.FollowUpState);
        Assert.Same(swipe, headbutt.FollowUpState);
        Assert.Collection(
            branch.States,
            state => AssertBranch(state, "SWIPE_RANDOM_MOVE", 0.4f),
            state => AssertBranch(state, "HEADBUTT_MOVE", 0.6f));

        int swipeRandomCount = 0;
        int headbuttCount = 0;
        for (int seed = 0; seed < 256; seed++)
        {
            Fogmog fogmog = CreateStructuralFogmog(ascension, $"fogmog-branch-{seed}");
            Assert.Equal("ILLUSION_MOVE", fogmog.NextMove!.StateId);
            fogmog.MoveStateMachine!.OnMovePerformed(fogmog.NextMove);
            fogmog.RollMove(fogmog.Creature.CombatState!.Allies);
            Assert.Equal("SWIPE_MOVE", fogmog.NextMove!.StateId);
            fogmog.MoveStateMachine.OnMovePerformed(fogmog.NextMove);
            fogmog.RollMove(fogmog.Creature.CombatState.Allies);
            if (fogmog.NextMove!.StateId == "SWIPE_RANDOM_MOVE")
            {
                swipeRandomCount++;
            }
            else
            {
                Assert.Equal("HEADBUTT_MOVE", fogmog.NextMove.StateId);
                headbuttCount++;
            }
        }

        Assert.InRange(swipeRandomCount, 75, 130);
        Assert.InRange(headbuttCount, 125, 180);
        Assert.Equal(256, swipeRandomCount + headbuttCount);
    }

    [Theory]
    [InlineData(0, 8, 14)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 9, 16)]
    public async Task SwipeVariantsGrantStrengthAndHeadbuttOnlyDealsDamage(
        int ascension,
        int expectedSwipeDamage,
        int expectedHeadbuttDamage)
    {
        (Fogmog fogmog, _, IReadOnlyList<Player> players) =
            await Task16MonsterTestFixture.CreateCombatAsync<Fogmog>(
                ascension,
                $"fogmog-effects-swipe-{ascension}",
                slotName: "fogmog");
        Player player = Assert.Single(players);
        ForceMove(fogmog, "SWIPE_MOVE");
        int hpBefore = player.Creature.CurrentHp;

        await fogmog.PerformMove();

        Assert.Equal(hpBefore - expectedSwipeDamage, player.Creature.CurrentHp);
        Assert.Equal(1, Assert.IsType<StrengthPower>(fogmog.Creature.GetPower<StrengthPower>()).Amount);

        (fogmog, _, players) = await Task16MonsterTestFixture.CreateCombatAsync<Fogmog>(
            ascension,
            $"fogmog-effects-random-swipe-{ascension}",
            slotName: "fogmog");
        player = Assert.Single(players);
        ForceMove(fogmog, "SWIPE_RANDOM_MOVE");
        hpBefore = player.Creature.CurrentHp;

        await fogmog.PerformMove();

        Assert.Equal(hpBefore - expectedSwipeDamage, player.Creature.CurrentHp);
        Assert.Equal(1, Assert.IsType<StrengthPower>(fogmog.Creature.GetPower<StrengthPower>()).Amount);

        (fogmog, _, players) = await Task16MonsterTestFixture.CreateCombatAsync<Fogmog>(
            ascension,
            $"fogmog-effects-headbutt-{ascension}",
            slotName: "fogmog");
        player = Assert.Single(players);
        ForceMove(fogmog, "HEADBUTT_MOVE");
        hpBefore = player.Creature.CurrentHp;

        await fogmog.PerformMove();

        Assert.Equal(hpBefore - expectedHeadbuttDamage, player.Creature.CurrentHp);
        Assert.Null(fogmog.Creature.GetPower<StrengthPower>());
    }

    [Fact]
    public async Task ProductionCombat_IllusionSummonsOneInitializedEyeThatActsRevivesAndDoesNotReplayCombatStart()
    {
        (Fogmog fogmog, var room, IReadOnlyList<Player> players) =
            await Task16MonsterTestFixture.CreateCombatAsync<Fogmog>(
                ascensionLevel: 0,
                seed: "fogmog-production-summon",
                playerCount: 2,
                slotName: "fogmog");
        int[] generatedBefore = players
            .Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat)
            .ToArray();
        Assert.Single(room.Engine.State.Enemies);
        Assert.Equal("ILLUSION_MOVE", fogmog.NextMove!.StateId);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, room.Engine.State.Enemies.Count);
        Assert.Same(fogmog.Creature, room.Engine.State.Enemies[0]);
        EyeWithTeeth eye = Assert.IsType<EyeWithTeeth>(room.Engine.State.Enemies[1].Monster);
        Assert.Equal("illusion", eye.Creature.SlotName);
        Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
        Assert.Equal(new[] { "DISTRACT_MOVE" }, eye.MoveStateMachine!.StateLog.Select(state => state.Id));
        Assert.Equal(1, Assert.IsType<IllusionPower>(eye.Creature.GetPower<IllusionPower>()).Amount);
        Assert.Equal(1, Assert.IsType<MinionPower>(eye.Creature.GetPower<MinionPower>()).Amount);
        Assert.Equal(
            generatedBefore,
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));
        Assert.Equal("SWIPE_MOVE", fogmog.NextMove!.StateId);

        int[] hpBeforeSwipe = players.Select(player => player.Creature.CurrentHp).ToArray();
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, room.Engine.State.Enemies.Count);
        Assert.Equal(
            hpBeforeSwipe.Select(hp => hp - 8),
            players.Select(player => player.Creature.CurrentHp));
        Assert.Equal(
            generatedBefore,
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));

        IllusionPower illusion = Assert.IsType<IllusionPower>(eye.Creature.GetPower<IllusionPower>());
        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { eye.Creature },
            eye.Creature.CurrentHp,
            ValueProp.Unblockable,
            dealer: players[0].Creature,
            cardSource: null,
            cardPlay: null);
        Assert.True(illusion.IsReviving);
        Assert.Equal("REVIVE_MOVE", eye.NextMove!.StateId);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, room.Engine.State.Enemies.Count);
        Assert.True(eye.Creature.IsAlive);
        Assert.Equal(eye.Creature.MaxHp, eye.Creature.CurrentHp);
        Assert.False(illusion.IsReviving);
        Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
        Assert.Equal(
            generatedBefore,
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, room.Engine.State.Enemies.Count);
        Assert.Equal(
            generatedBefore,
            players.Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat));
        Assert.Equal("DISTRACT_MOVE", eye.NextMove!.StateId);
        Assert.NotEqual("ILLUSION_MOVE", fogmog.NextMove!.StateId);
    }

    [Fact]
    public async Task KillingFogmogAlsoKillsItsLivingSecondaryIllusionAndEndsCombat()
    {
        (Fogmog fogmog, var room, IReadOnlyList<Player> players) =
            await Task16MonsterTestFixture.CreateCombatAsync<Fogmog>(
                ascensionLevel: 0,
                seed: "fogmog-primary-death-cleanup");
        ForceMove(fogmog, "ILLUSION_MOVE");
        await room.Engine.EndPlayerTurnAsync();
        EyeWithTeeth eye = Assert.IsType<EyeWithTeeth>(
            room.Engine.State.Enemies.Single(enemy => enemy.Monster is EyeWithTeeth).Monster);

        DamageResult killed = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { fogmog.Creature },
            fogmog.Creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: players[0].Creature,
            cardSource: null,
            cardPlay: null));

        Assert.True(killed.WasTargetKilled);
        Assert.True(fogmog.Creature.IsDead);
        Assert.True(eye.Creature.IsDead);
        Assert.True(room.Engine.CheckWinCondition());
        Assert.True(room.Engine.Won);
    }
    private static Fogmog CreateStructuralFogmog(int ascension, string seed)
    {
        var runState = new RunState(seed, new Overgrowth(), ascension);
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(Creature.CreateStandaloneForTests(100, 100));
        var fogmog = (Fogmog)ModelDb.Monster<Fogmog>().MutableClone();
        combatState.AddMonster(fogmog, CombatSide.Enemy, "fogmog");
        fogmog.SetUpForCombat();
        fogmog.RollMove(combatState.Allies);
        return fogmog;
    }

    private static void AssertBranch(
        RandomBranchState.StateWeight state,
        string expectedStateId,
        float expectedWeight)
    {
        Assert.Equal(expectedStateId, state.StateId);
        Assert.Equal(MoveRepeatType.CannotRepeat, state.RepeatType);
        Assert.Equal(0, state.Cooldown);
        Assert.Equal(expectedWeight, state.GetWeight());
    }

    private static void ForceMove(Fogmog fogmog, string stateId) =>
        fogmog.SetMoveImmediate(
            Assert.IsType<MoveState>(fogmog.MoveStateMachine!.States[stateId]),
            forceTransition: true);
}
