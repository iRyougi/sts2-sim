using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.MonsterMoves;

namespace Sts2Sim.Core.Tests.Combat;

[Collection("ModelDb")]
public sealed class SummonedEnemyHpFidelityTests : IDisposable
{
    public SummonedEnemyHpFidelityTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("LivingFogNormal", 0)]
    [InlineData("LivingFogNormal", 10)]
    [InlineData("GremlinMercNormal", 0)]
    [InlineData("GremlinMercNormal", 10)]
    public async Task Summon_MatchesNativeCreationHpAndEnemyOrder(string name, int ascension)
    {
        const string seed = "VJBR4G6SX670";
        var run = new RunState(seed, new Underdocks(), ascension);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var encounter = UnderdocksEncounters.BatchA.Single(e => e.Name == name);
        var combat = new CombatState(run, encounter.Slots);
        combat.AddPlayerCreature(player.Creature); // Native rooms attach the player before initial monsters.
        foreach (var (monster, slot) in encounter.CreateMonsters())
            combat.AddMonster(monster, CombatSide.Enemy, slot);
        var engine = new CombatEngine(combat);
        await engine.StartCombatAsync();
        Creature parent = Assert.Single(combat.Enemies);
        var oracle = run.Rng.Niche.CloneExact();
        int before = run.Rng.Niche.Counter;
        if (name == "LivingFogNormal")
        {
            Assert.Equal(new[] { "bomb1", "bomb2", "bomb3", "bomb4", "bomb5", "livingFog" }, encounter.Slots);
            // Native Bloat uses the encounter's first unoccupied slot; new minions precede the parent.
            combat.CurrentSide = CombatSide.Enemy;
            var fog = Assert.IsType<LivingFog>(parent.Monster);
            fog.SetMoveImmediate((MoveState)fog.MoveStateMachine!.States["BLOAT_MOVE"], forceTransition: true);
            oracle.NextInt(0, 1); // fixed HP still consumes one native Niche draw
            await fog.PerformMove();
            Assert.Equal(new[] { "bomb1", "livingFog" }, combat.Enemies.Select(c => c.SlotName));
            Creature bomb = combat.Enemies[0];
            Assert.IsType<GasBomb>(bomb.Monster);
            Assert.Equal(ascension == 10 ? 8 : 7, bomb.CurrentHp);
            Assert.Equal(2u, bomb.CombatId);
            Assert.Same(parent, combat.Enemies[1]);
            Assert.NotNull(bomb.GetPower<MinionPower>());
            Assert.Same(combat, bomb.CombatState);
            Assert.Equal(before + 1, run.Rng.Niche.Counter);
        }
        else
        {
            // Native Surprise creates fat first, before either new gremlin is added to the side.
            int expectedFat = (ascension == 10 ? 14 : 13) + oracle.NextInt(0, 5);
            int expectedSneaky = (ascension == 10 ? 11 : 10) + oracle.NextInt(0, 5);
            ThieveryPower theft = Assert.Single(parent.Powers.OfType<ThieveryPower>());
            await theft.Steal();
            await CreatureCmd.Kill(parent);
            Assert.Equal(new[] { "sneaky", "fat" }, combat.Enemies.Select(c => c.SlotName));
            Creature sneaky = combat.Enemies[0];
            Creature fat = combat.Enemies[1];
            Assert.IsType<SneakyGremlin>(sneaky.Monster);
            Assert.IsType<FatGremlin>(fat.Monster);
            Assert.Equal(expectedFat, fat.CurrentHp);
            Assert.Equal(expectedSneaky, sneaky.CurrentHp);
            Assert.Equal(2u, fat.CombatId);
            Assert.Equal(3u, sneaky.CombatId);
            Assert.Equal(before + 2, run.Rng.Niche.Counter);
            HeistPower heist = Assert.Single(fat.Powers.OfType<HeistPower>());
            Assert.Equal(theft.GoldStolen, heist.Amount);
            Assert.Same(player.Creature, heist.Target);
            Assert.All(combat.Enemies, c =>
            {
                Assert.Same(combat, c.CombatState);
                Assert.Equal("SPAWNED_MOVE", c.Monster!.NextMove!.Id);
            });
        }
        Assert.True(oracle.NextInt() == run.Rng.Niche.NextInt(), $"{name}, A{ascension}, seed={seed}: RNG continuation differs");
    }
}
