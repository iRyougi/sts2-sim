namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
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
public sealed class PhrogParasiteTests : IDisposable
{
    private readonly Task19MonsterTestFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Theory]
    [InlineData(0, 61, 64)]
    [InlineData((int)AscensionLevel.ToughEnemies, 66, 68)]
    public async Task HpOpeningSlotAndLifecyclePower_UseAuthoritativeValues(
        int ascension,
        int expectedMinHp,
        int expectedMaxHp)
    {
        (PhrogParasite phrog, _, _) = await Task19MonsterTestFixture.CreateCombatAsync(
            ascension,
            $"phrog-hp-{ascension}");

        Assert.Equal(expectedMinHp, phrog.MinInitialHp);
        Assert.Equal(expectedMaxHp, phrog.MaxInitialHp);
        Assert.InRange(phrog.Creature.MaxHp, expectedMinHp, expectedMaxHp);
        Assert.Equal("phrog", phrog.Creature.SlotName);
        Assert.Equal("INFECT_MOVE", phrog.NextMove!.StateId);
        InfestedPower infested = Assert.IsType<InfestedPower>(
            phrog.Creature.GetPower<InfestedPower>());
        Assert.Equal(4, infested.Amount);
        Assert.Same(phrog.Creature, infested.Applier);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 5)]
    public async Task StateGraph_HasOnlyFixedInfectLashCycleAndExactIntents(
        int ascension,
        int expectedLashDamage)
    {
        (PhrogParasite phrog, _, _) = await Task19MonsterTestFixture.CreateCombatAsync(
            ascension,
            $"phrog-structure-{ascension}");
        MonsterMoveStateMachine machine = phrog.MoveStateMachine!;
        MoveState infect = Assert.IsType<MoveState>(machine.States["INFECT_MOVE"]);
        MoveState lash = Assert.IsType<MoveState>(machine.States["LASH_MOVE"]);

        Assert.Equal(2, machine.States.Count);
        Assert.DoesNotContain("RAND", machine.States.Keys);
        Assert.Equal(3, Assert.IsType<StatusIntent>(Assert.Single(infect.Intents)).Count);
        MultiAttackIntent lashIntent = Assert.IsType<MultiAttackIntent>(
            Assert.Single(lash.Intents));
        Assert.Equal(expectedLashDamage,
            lashIntent.GetSingleDamage(PlayerTarget, phrog.Creature));
        Assert.Equal(4, lashIntent.Repeats);
        Assert.Same(lash, infect.FollowUpState);
        Assert.Same(infect, lash.FollowUpState);

        Assert.Equal("INFECT_MOVE", phrog.NextMove!.StateId);
        RollAfterPerformed(phrog);
        Assert.Equal("LASH_MOVE", phrog.NextMove!.StateId);
        RollAfterPerformed(phrog);
        Assert.Equal("INFECT_MOVE", phrog.NextMove!.StateId);
    }

    [Fact]
    public async Task Infect_GeneratesThreeOwnedCombatInfectionsIntoEveryPlayerDiscard()
    {
        (PhrogParasite phrog, var room, IReadOnlyList<Player> players) =
            await Task19MonsterTestFixture.CreateCombatAsync(
                0,
                "phrog-infect-multiplayer",
                playerCount: 2);
        int[] generatedBefore = players
            .Select(player => player.PlayerCombatState!.CardsGeneratedThisCombat)
            .ToArray();
        Task19MonsterTestFixture.ForceMove(phrog, "INFECT_MOVE");

        await phrog.PerformMove();

        for (int playerIndex = 0; playerIndex < players.Count; playerIndex++)
        {
            Player player = players[playerIndex];
            Infection[] infections = player.PlayerCombatState!.DiscardPile.Cards
                .OfType<Infection>()
                .ToArray();
            Assert.Equal(3, infections.Length);
            Assert.Equal(3, infections.Distinct(ReferenceEqualityComparer.Instance).Count());
            Assert.All(infections, infection =>
            {
                Assert.Same(player, infection.Owner);
                Assert.Same(player.PlayerCombatState.DiscardPile, infection.Pile);
                Assert.Same(room.Engine.State, infection.CombatState);
            });
            Assert.Equal(
                generatedBefore[playerIndex] + 3,
                player.PlayerCombatState.CardsGeneratedThisCombat);
        }
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData((int)AscensionLevel.DeadlyEnemies, 20)]
    public async Task Lash_DealsFourHitsToEveryPlayerThroughCombatCommands(
        int ascension,
        int expectedTotalLoss)
    {
        (PhrogParasite phrog, _, IReadOnlyList<Player> players) =
            await Task19MonsterTestFixture.CreateCombatAsync(
                ascension,
                $"phrog-lash-{ascension}",
                playerCount: 2);
        int[] hpBefore = players.Select(player => player.Creature.CurrentHp).ToArray();
        Task19MonsterTestFixture.ForceMove(phrog, "LASH_MOVE");

        await phrog.PerformMove();

        Assert.Equal(hpBefore.Select(hp => hp - expectedTotalLoss),
            players.Select(player => player.Creature.CurrentHp));
    }

    [Fact]
    public async Task ProductionDeath_SpawnsFourStunnedWrigglersThatMustAllDieForVictory()
    {
        (PhrogParasite phrog, var room, IReadOnlyList<Player> players) =
            await Task19MonsterTestFixture.CreateCombatAsync(
                0,
                "phrog-production-death",
                playerCount: 1);
        Player player = Assert.Single(players);
        Creature phrogCreature = Assert.Single(room.Engine.State.Enemies);
        Assert.Same(phrog, phrogCreature.Monster);
        Assert.Equal("phrog", phrogCreature.SlotName);
        Assert.Equal(4, Assert.IsType<InfestedPower>(
            phrogCreature.GetPower<InfestedPower>()).Amount);

        DamageResult killed = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { phrogCreature },
            phrogCreature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: player.Creature,
            cardSource: null,
            cardPlay: null));

        Assert.True(killed.WasTargetKilled);
        Assert.True(phrogCreature.IsDead);
        Assert.Null(phrogCreature.GetPower<InfestedPower>());
        Assert.False(room.Engine.CheckWinCondition());
        Assert.True(room.Engine.IsInProgress);
        Assert.False(room.Engine.Won);
        Creature[] wrigglerCreatures = room.Engine.State.Enemies
            .Where(enemy => enemy.Monster is Wriggler)
            .ToArray();
        Assert.Equal(4, wrigglerCreatures.Length);
        Assert.Equal(
            new[] { "wriggler1", "wriggler2", "wriggler3", "wriggler4" },
            wrigglerCreatures.Select(wriggler => wriggler.SlotName));
        Assert.All(wrigglerCreatures, wrigglerCreature =>
        {
            Wriggler wriggler = Assert.IsType<Wriggler>(wrigglerCreature.Monster);
            Assert.True(wriggler.StartStunned);
            Assert.Equal("SPAWNED_MOVE", wriggler.NextMove!.StateId);
        });

        int hpBeforeSpawnedTurn = player.Creature.CurrentHp;
        int generatedBeforeSpawnedTurn = player.PlayerCombatState!.CardsGeneratedThisCombat;
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(hpBeforeSpawnedTurn, player.Creature.CurrentHp);
        Assert.Equal(
            generatedBeforeSpawnedTurn,
            player.PlayerCombatState.CardsGeneratedThisCombat);
        Assert.All(wrigglerCreatures, wrigglerCreature =>
            Assert.Null(wrigglerCreature.GetPower<StrengthPower>()));
        Assert.Equal(
            new[] { "NASTY_BITE_MOVE", "WRIGGLE_MOVE", "NASTY_BITE_MOVE", "WRIGGLE_MOVE" },
            wrigglerCreatures.Select(wriggler => wriggler.Monster!.NextMove!.StateId));

        int hpBeforeNaturalTurn = player.Creature.CurrentHp;
        int generatedBeforeNaturalTurn = player.PlayerCombatState.CardsGeneratedThisCombat;
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(hpBeforeNaturalTurn - 12, player.Creature.CurrentHp);
        Assert.Equal(
            generatedBeforeNaturalTurn + 2,
            player.PlayerCombatState.CardsGeneratedThisCombat);
        Assert.Equal(2, wrigglerCreatures[1].GetPower<StrengthPower>()!.Amount);
        Assert.Equal(2, wrigglerCreatures[3].GetPower<StrengthPower>()!.Amount);
        Assert.Equal(
            new[] { "WRIGGLE_MOVE", "NASTY_BITE_MOVE", "WRIGGLE_MOVE", "NASTY_BITE_MOVE" },
            wrigglerCreatures.Select(wriggler => wriggler.Monster!.NextMove!.StateId));

        for (int index = 0; index < wrigglerCreatures.Length; index++)
        {
            Creature wriggler = wrigglerCreatures[index];
            await CreatureCmd.Damage(
                room.Engine.State,
                new[] { wriggler },
                wriggler.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: player.Creature,
                cardSource: null,
                cardPlay: null);
            bool ended = room.Engine.CheckWinCondition();
            Assert.Equal(index == wrigglerCreatures.Length - 1, ended);
        }

        Assert.True(room.Engine.Won);
        Assert.False(room.Engine.IsInProgress);
    }

    [Fact]
    public async Task PreventedDeath_RevivesPhrogWithoutFiringInfestedSummon()
    {
        (PhrogParasite phrog, var room, IReadOnlyList<Player> players) =
            await Task19MonsterTestFixture.CreateCombatAsync(
                0,
                "phrog-prevented-death");
        Player player = Assert.Single(players);
        await PowerCmd.Apply<PhrogDeathPreventionPower>(
            room.Engine.State,
            phrog.Creature,
            1m,
            phrog.Creature,
            cardSource: null);

        DamageResult result = Assert.Single(await CreatureCmd.Damage(
            room.Engine.State,
            new[] { phrog.Creature },
            phrog.Creature.CurrentHp,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer: player.Creature,
            cardSource: null,
            cardPlay: null));

        Assert.True(result.WasTargetKilled);
        Assert.True(phrog.Creature.IsAlive);
        Assert.Equal(1, phrog.Creature.CurrentHp);
        Assert.Single(room.Engine.State.Enemies);
        Assert.DoesNotContain(
            room.Engine.State.Enemies, enemy => enemy.Monster is Wriggler);
        Assert.Equal(4, phrog.Creature.GetPower<InfestedPower>()!.Amount);
        Assert.False(room.Engine.CheckWinCondition());
    }

    private static IReadOnlyList<Creature> PlayerTarget { get; } =
        new[] { Creature.CreateStandaloneForTests(100, 100) };

    private static void RollAfterPerformed(PhrogParasite phrog)
    {
        phrog.MoveStateMachine!.OnMovePerformed(phrog.NextMove!);
        phrog.RollMove(phrog.Creature.CombatState!.Allies);
    }
}
