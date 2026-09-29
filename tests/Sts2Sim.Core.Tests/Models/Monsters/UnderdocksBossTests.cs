namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class UnderdocksBossTests : IDisposable
{
    public UnderdocksBossTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("SoulFysh", 211, "BECKON_MOVE", 5, 2)]
    [InlineData("LagavulinMatriarch", 222, "SLEEP_MOVE", 6, 3)]
    [InlineData("BossRoster", 0, "LagavulinMatriarchBoss", 3, 0)]
    public async Task BossFamiliesAndRoster_UseAuthoritativeValues(
        string subject,
        int expectedHp,
        string expectedOpeningMove,
        int expectedStateCount,
        int expectedOpeningAmount)
    {
        if (subject == "BossRoster")
        {
            Assert.Equal(
                new[] { "LagavulinMatriarchBoss", "SoulFyshBoss", "WaterfallGiantBoss" },
                UnderdocksEncounters.Bosses.Select(encounter => encounter.Name));

            IReadOnlyList<(MonsterModel Monster, string? SlotName)> monsters =
                UnderdocksEncounters.Bosses.SelectMany(encounter => encounter.CreateMonsters()).ToArray();

            Assert.Equal(
                new[] { "LagavulinMatriarch", "SoulFysh", "WaterfallGiant" },
                monsters.Select(entry => entry.Monster.GetType().Name));
            Assert.Equal(3, monsters.Count);
            Assert.All(monsters, entry => Assert.Null(entry.SlotName));
            return;
        }

        if (subject == "SoulFysh")
        {
            (SoulFysh soulFysh, _, IReadOnlyList<Player> soulPlayers) =
                await Task20BossTestFixture.CreateSingleCombatAsync<SoulFysh>(
                    0, $"underdocks-boss-{subject}-{expectedOpeningAmount}");
            Player soulPlayer = Assert.Single(soulPlayers);

            Assert.Equal(expectedHp, soulFysh.MinInitialHp);
            Assert.Equal(expectedHp, soulFysh.MaxInitialHp);
            Assert.Equal(expectedOpeningMove, soulFysh.NextMove!.StateId);
            Assert.Equal(expectedStateCount, soulFysh.MoveStateMachine!.States.Count);
            Assert.IsType<StatusIntent>(Assert.Single(soulFysh.NextMove!.Intents));
            Assert.Equal(expectedOpeningAmount, Assert.IsType<StatusIntent>(soulFysh.NextMove!.Intents[0]).Count);
            Assert.Equal(
                new[] { "BECKON_MOVE", "DE_GAS_MOVE", "GAZE_MOVE", "SCREAM_MOVE", "FADE_MOVE" },
                soulFysh.MoveStateMachine!.States.Keys);
            Assert.Same(soulFysh.MoveStateMachine!.States["DE_GAS_MOVE"],
                Assert.IsType<MoveState>(soulFysh.MoveStateMachine!.States["BECKON_MOVE"]).FollowUpState);
            Assert.Same(soulFysh.MoveStateMachine!.States["GAZE_MOVE"],
                Assert.IsType<MoveState>(soulFysh.MoveStateMachine!.States["DE_GAS_MOVE"]).FollowUpState);
            Assert.Same(soulFysh.MoveStateMachine!.States["FADE_MOVE"],
                Assert.IsType<MoveState>(soulFysh.MoveStateMachine!.States["GAZE_MOVE"]).FollowUpState);
            Assert.Same(soulFysh.MoveStateMachine!.States["SCREAM_MOVE"],
                Assert.IsType<MoveState>(soulFysh.MoveStateMachine!.States["FADE_MOVE"]).FollowUpState);
            Assert.Same(soulFysh.MoveStateMachine!.States["BECKON_MOVE"],
                Assert.IsType<MoveState>(soulFysh.MoveStateMachine!.States["SCREAM_MOVE"]).FollowUpState);

            PlayerCombatState soulCombat = soulPlayer.PlayerCombatState!;
            int drawCountBefore = soulCombat.DrawPile.Cards.Count;
            int discardCountBefore = soulCombat.DiscardPile.Cards.Count;
            int generatedCountBefore = soulCombat.CardsGeneratedThisCombat;
            CardModel[] originalDrawCards = soulCombat.DrawPile.Cards.ToArray();

            await soulFysh.PerformMove();

            Assert.Equal(drawCountBefore + 1, soulCombat.DrawPile.Cards.Count);
            Assert.Equal(discardCountBefore + 1, soulCombat.DiscardPile.Cards.Count);
            Assert.Equal(generatedCountBefore + 2, soulCombat.CardsGeneratedThisCombat);
            Beckon drawBeckon = Assert.Single(soulCombat.DrawPile.Cards.OfType<Beckon>());
            Beckon discardBeckon = Assert.Single(soulCombat.DiscardPile.Cards.OfType<Beckon>());
            Assert.Same(soulPlayer, drawBeckon.Owner);
            Assert.Same(soulPlayer, discardBeckon.Owner);
            Assert.InRange(soulCombat.DrawPile.Cards.ToList().IndexOf(drawBeckon), 0, drawCountBefore);
            Assert.Same(discardBeckon, soulCombat.DiscardPile.Cards[^1]);
            Assert.DoesNotContain(originalDrawCards, card => ReferenceEquals(card, drawBeckon));

            Task20BossTestFixture.RollAfterPerformed(soulFysh);
            Assert.Equal("DE_GAS_MOVE", soulFysh.NextMove!.StateId);

            (SoulFysh soulFyshA9, CombatRoom soulFyshA9Room, _) =
                await Task20BossTestFixture.CreateSingleCombatAsync<SoulFysh>(
                    9, $"underdocks-boss-{subject}-a9-clone");
            Assert.Equal(18, Assert.IsType<MoveState>(soulFyshA9.MoveStateMachine!.States["DE_GAS_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                soulFyshA9.Creature.CombatState!.Allies, soulFyshA9.Creature));
            Assert.Equal(8, Assert.IsType<MoveState>(soulFyshA9.MoveStateMachine.States["GAZE_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                soulFyshA9.Creature.CombatState.Allies, soulFyshA9.Creature));
            Assert.Equal(15, Assert.IsType<MoveState>(soulFyshA9.MoveStateMachine.States["SCREAM_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                soulFyshA9.Creature.CombatState.Allies, soulFyshA9.Creature));

            CombatState soulFyshClone = soulFyshA9Room.Engine.State.Clone();
            SoulFysh clonedSoulFysh = Assert.IsType<SoulFysh>(Assert.Single(soulFyshClone.Enemies).Monster);
            Assert.Equal(18, Assert.IsType<MoveState>(clonedSoulFysh.MoveStateMachine!.States["DE_GAS_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                soulFyshClone.Allies, clonedSoulFysh.Creature));
            Assert.Equal(8, Assert.IsType<MoveState>(clonedSoulFysh.MoveStateMachine.States["GAZE_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                soulFyshClone.Allies, clonedSoulFysh.Creature));
            Assert.Equal(15, Assert.IsType<MoveState>(clonedSoulFysh.MoveStateMachine.States["SCREAM_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                soulFyshClone.Allies, clonedSoulFysh.Creature));
            Task20BossTestFixture.ForceMove(soulFyshA9, "DE_GAS_MOVE");
            Creature victim = soulFyshA9Room.Engine.State.Players[0].Creature;
            int hpBeforeDeGas = victim.CurrentHp;
            await soulFyshA9.PerformMove();
            Assert.Equal(hpBeforeDeGas - 18, victim.CurrentHp);
        }
        else
        {
            (LagavulinMatriarch lagavulin, CombatRoom lagavulinRoom, IReadOnlyList<Player> lagavulinPlayers) =
                await Task20BossTestFixture.CreateSingleCombatAsync<LagavulinMatriarch>(
                    0, $"underdocks-boss-{subject}-{expectedOpeningAmount}");
            Player lagavulinPlayer = Assert.Single(lagavulinPlayers);

            Assert.Equal(expectedHp, lagavulin.MinInitialHp);
            Assert.Equal(expectedHp, lagavulin.MaxInitialHp);
            Assert.Equal(expectedOpeningMove, lagavulin.NextMove!.StateId);
            Assert.Equal(expectedStateCount, lagavulin.MoveStateMachine!.States.Count);
            Assert.False(lagavulin.IsAwake);
            AsleepPower asleep = Assert.IsType<AsleepPower>(lagavulin.Creature.GetPower<AsleepPower>());
            Assert.Equal(expectedOpeningAmount, asleep.Amount);
            Assert.Equal(12, Assert.IsType<PlatingPower>(lagavulin.Creature.GetPower<PlatingPower>()).Amount);
            Assert.IsType<SleepIntent>(Assert.Single(lagavulin.NextMove!.Intents));
            Assert.IsType<ConditionalBranchState>(lagavulin.MoveStateMachine!.States["SLEEP_BRANCH"]);
            Assert.Same(lagavulin.MoveStateMachine!.States["SLEEP_BRANCH"],
                Assert.IsType<MoveState>(lagavulin.MoveStateMachine!.States["SLEEP_MOVE"]).FollowUpState);

            await lagavulinRoom.Engine.EndPlayerTurnAsync();
            Assert.Equal(expectedOpeningAmount - 1,
                Assert.IsType<AsleepPower>(lagavulin.Creature.GetPower<AsleepPower>()).Amount);

            DamageResult wakeHit = Assert.Single(await CreatureCmd.Damage(
                lagavulinRoom.Engine.State,
                new[] { lagavulin.Creature },
                1m,
                ValueProp.Unblockable | ValueProp.Unpowered,
                lagavulinPlayer.Creature,
                null,
                null));
            Assert.Equal(1, wakeHit.UnblockedDamage);
            Assert.True(lagavulin.IsAwake);
            Assert.Null(lagavulin.Creature.GetPower<AsleepPower>());
            Assert.Null(lagavulin.Creature.GetPower<PlatingPower>());
            Assert.Equal("STUNNED", lagavulin.NextMove!.StateId);

            await lagavulinRoom.Engine.EndPlayerTurnAsync();
            Assert.Equal("SLASH_MOVE", lagavulin.NextMove!.StateId);

            (LagavulinMatriarch lagavulinA9, CombatRoom lagavulinA9Room, _) =
                await Task20BossTestFixture.CreateSingleCombatAsync<LagavulinMatriarch>(
                    9, $"underdocks-boss-{subject}-a9-clone");
            Assert.Equal(21, Assert.IsType<MoveState>(lagavulinA9.MoveStateMachine!.States["SLASH_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                lagavulinA9.Creature.CombatState!.Allies, lagavulinA9.Creature));
            Assert.Equal(14, Assert.IsType<MoveState>(lagavulinA9.MoveStateMachine.States["SLASH2_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                lagavulinA9.Creature.CombatState.Allies, lagavulinA9.Creature));
            Assert.Equal(10, Assert.IsType<MoveState>(lagavulinA9.MoveStateMachine.States["DISEMBOWEL_MOVE"]).Intents.OfType<MultiAttackIntent>().Single().GetSingleDamage(
                lagavulinA9.Creature.CombatState.Allies, lagavulinA9.Creature));

            CombatState lagavulinClone = lagavulinA9Room.Engine.State.Clone();
            LagavulinMatriarch clonedLagavulin =
                Assert.IsType<LagavulinMatriarch>(Assert.Single(lagavulinClone.Enemies).Monster);
            Assert.Equal(21, Assert.IsType<MoveState>(clonedLagavulin.MoveStateMachine!.States["SLASH_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                lagavulinClone.Allies, clonedLagavulin.Creature));
            Assert.Equal(14, Assert.IsType<MoveState>(clonedLagavulin.MoveStateMachine.States["SLASH2_MOVE"]).Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                lagavulinClone.Allies, clonedLagavulin.Creature));
            Assert.Equal(10, Assert.IsType<MoveState>(clonedLagavulin.MoveStateMachine.States["DISEMBOWEL_MOVE"]).Intents.OfType<MultiAttackIntent>().Single().GetSingleDamage(
                lagavulinClone.Allies, clonedLagavulin.Creature));
        }
    }
}
