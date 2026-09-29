using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Monsters;

[Collection("ModelDb")]
public sealed class SlimedBerserkerSourceTests : IDisposable
{
    public SlimedBerserkerSourceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task NormalGloryHug_GivesSilentWeakWithoutApplierAndKeepsSelfStrengthSource()
    {
        const string seed = "09-S7-131f-silent-a10";
        var run = new RunState(seed, [new Overgrowth(), new Hive(), new Glory()], ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.AdvanceToNextAct();
        run.AdvanceToNextAct();

        var encounter = GloryEncounters.BatchB.Single(e => e.Name == "SlimedBerserkerNormal");
        var combat = new CombatState(run);
        combat.AddPlayerCreature(player.Creature);
        foreach (var (spawned, slot) in encounter.CreateMonsters())
            combat.AddMonster(spawned, CombatSide.Enemy, slot);
        var engine = new CombatEngine(combat);
        await engine.StartCombatAsync();
        var monster = Assert.IsType<SlimedBerserker>(Assert.Single(combat.Enemies).Monster);

        string[] moves = ["VOMIT_ICHOR_MOVE", "FURIOUS_PUMMELING_MOVE", "LEECHING_HUG_MOVE"];
        foreach (string move in moves)
        {
            Assert.Equal(move, monster.NextMove?.StateId);
            await monster.PerformMove();
            if (move != "LEECHING_HUG_MOVE")
                monster.RollMove(combat.PlayerCreatures);
        }

        Assert.True(player.Creature.IsAlive, $"seed={seed}: Silent must survive to Hug");
        WeakPower weak = Assert.IsType<WeakPower>(player.Creature.GetPower<WeakPower>());
        StrengthPower strength = Assert.IsType<StrengthPower>(monster.Creature.GetPower<StrengthPower>());
        Assert.Equal(3m, weak.Amount);
        Assert.Null(weak.Applier);
        Assert.Equal(3m, strength.Amount);
        Assert.Same(monster.Creature, strength.Applier);

        CombatStateDescriptionDigest nativeSourceDigest = Digest(combat);
        weak.Applier = monster.Creature;
        Assert.NotEqual(nativeSourceDigest, Digest(combat));
    }

    private static CombatStateDescriptionDigest Digest(CombatState combat)
    {
        var builder = new CombatStateDescriptionBuilder();
        CombatStateDescription.AppendExactState(ref builder, combat);
        return builder.Build();
    }
}
