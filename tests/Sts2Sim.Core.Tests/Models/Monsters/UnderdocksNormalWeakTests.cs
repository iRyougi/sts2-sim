namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class UnderdocksNormalWeakTests : IDisposable
{
    public UnderdocksNormalWeakTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("corpse", "WHIP_SLAP_MOVE", 25, 27, -1, false, 0)]
    [InlineData("calcified", "INCANTATION_MOVE", 38, 41, -1, false, 0)]
    [InlineData("damp", "INCANTATION_MOVE", 51, 53, -1, false, 0)]
    [InlineData("fossil", "LATCH_MOVE", 51, 53, -1, false, 0)]
    [InlineData("merc", "GIMME_MOVE", 47, 49, -1, false, 0)]
    [InlineData("fat", "SPAWNED_MOVE", 13, 17, -1, false, 0)]
    [InlineData("sneaky", "SPAWNED_MOVE", 10, 14, -1, false, 0)]
    [InlineData("ship", "HAUNT_MOVE", 63, 63, -1, false, 0)]
    [InlineData("fog", "ADVANCED_GAS_MOVE", 82, 82, -1, false, 9)]
    [InlineData("bomb", "EXPLODE_MOVE", 7, 7, -1, false, 0)]
    [InlineData("seapunk", "SEA_KICK_MOVE", 44, 46, -1, false, 0)]
    [InlineData("clam", "JET_MOVE", 56, 56, -1, false, 0)]
    [InlineData("rat", "SCRATCH_MOVE", 17, 21, 0, false, 0)]
    [InlineData("sludge", "OIL_SPRAY_MOVE", 37, 39, -1, false, 0)]
    [InlineData("toadpole", "SPIKEN_MOVE", 21, 25, -1, true, 0)]
    [InlineData("punch", "READY_MOVE", 55, 55, -1, false, 0)]
    public async Task NormalAndWeakFamilies_UseAuthoritativeHpAndInitialMove(
        string family,
        string expectedMove,
        int expectedMinHp,
        int expectedMaxHp,
        int starterMoveIndex,
        bool isFront,
        int ascension)
    {
        MonsterModel monster = family switch
        {
            "corpse" => (CorpseSlug)ModelDb.Monster<CorpseSlug>().MutableClone(),
            "calcified" => (CalcifiedCultist)ModelDb.Monster<CalcifiedCultist>().MutableClone(),
            "damp" => (DampCultist)ModelDb.Monster<DampCultist>().MutableClone(),
            "fossil" => (FossilStalker)ModelDb.Monster<FossilStalker>().MutableClone(),
            "merc" => (GremlinMerc)ModelDb.Monster<GremlinMerc>().MutableClone(),
            "fat" => (FatGremlin)ModelDb.Monster<FatGremlin>().MutableClone(),
            "sneaky" => (SneakyGremlin)ModelDb.Monster<SneakyGremlin>().MutableClone(),
            "ship" => (HauntedShip)ModelDb.Monster<HauntedShip>().MutableClone(),
            "fog" => (LivingFog)ModelDb.Monster<LivingFog>().MutableClone(),
            "bomb" => (GasBomb)ModelDb.Monster<GasBomb>().MutableClone(),
            "seapunk" => (Seapunk)ModelDb.Monster<Seapunk>().MutableClone(),
            "clam" => (SewerClam)ModelDb.Monster<SewerClam>().MutableClone(),
            "rat" => (TwoTailedRat)ModelDb.Monster<TwoTailedRat>().MutableClone(),
            "sludge" => (SludgeSpinner)ModelDb.Monster<SludgeSpinner>().MutableClone(),
            "toadpole" => (Toadpole)ModelDb.Monster<Toadpole>().MutableClone(),
            "punch" => (PunchConstruct)ModelDb.Monster<PunchConstruct>().MutableClone(),
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null),
        };

        if (monster is TwoTailedRat rat && starterMoveIndex >= 0)
        {
            rat.StarterMoveIndex = starterMoveIndex;
        }

        if (monster is Toadpole toadpole)
        {
            toadpole.IsFront = isFront;
        }

        var run = new RunState($"underdocks-normal-weak-{family}", new Overgrowth(), ascension);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        player.ResetCombatState();
        var state = new CombatState(run);
        Creature target = player.Creature;
        state.AddPlayerCreature(target);
        state.AddMonster(monster, CombatSide.Enemy);
        monster.SetUpForCombat();
        if (monster is PunchConstruct punch)
        {
            await punch.BeforeCombatStart();
        }
        await monster.AfterAddedToRoom();
        monster.RollMove(state.Allies);

        Assert.Equal(expectedMinHp, monster.MinInitialHp);
        Assert.Equal(expectedMaxHp, monster.MaxInitialHp);
        Assert.Equal(expectedMove, monster.NextMove!.StateId);

        int targetHpBefore = target.CurrentHp;
        switch (monster)
        {
            case CorpseSlug corpse:
            {
                Assert.Equal(4, corpse.Creature.GetPower<RavenousPower>()!.Amount);
                Assert.Equal(2, Assert.IsType<MultiAttackIntent>(Assert.Single(corpse.NextMove!.Intents)).Repeats);

                var companion = (CorpseSlug)ModelDb.Monster<CorpseSlug>().MutableClone();
                state.AddMonster(companion, CombatSide.Enemy);
                companion.SetUpForCombat();
                await companion.AfterAddedToRoom();
                companion.RollMove(state.Allies);
                await CreatureCmd.Kill(companion.Creature);

                Assert.True(corpse.IsRavenous);
                Assert.Equal("STUNNED", corpse.NextMove!.StateId);
                CombatState corpseProjection = state.Clone();
                CorpseSlug cloneCorpse = Assert.IsType<CorpseSlug>(
                    corpseProjection.Enemies.Single(creature => creature.IsAlive).Monster);
                Assert.True(cloneCorpse.IsRavenous);
                Assert.Equal("STUNNED", cloneCorpse.NextMove!.StateId);
                await cloneCorpse.PerformMove();
                Assert.False(cloneCorpse.IsRavenous);
                Assert.True(corpse.IsRavenous);

                await corpse.PerformMove();
                Assert.False(corpse.IsRavenous);
                corpse.RollMove(state.Allies);
                await corpse.PerformMove();
                Assert.Equal(14, targetHpBefore - target.CurrentHp);
                break;
            }
            case CalcifiedCultist calcified:
                await calcified.PerformMove();
                Assert.Equal(2, calcified.Creature.GetPower<RitualPower>()!.Amount);
                break;
            case DampCultist damp:
                await damp.PerformMove();
                Assert.Equal(5, damp.Creature.GetPower<RitualPower>()!.Amount);
                break;
            case FossilStalker fossil:
                Assert.Equal(3, fossil.Creature.GetPower<SuckPower>()!.Amount);
                await fossil.PerformMove();
                Assert.Equal(12, targetHpBefore - target.CurrentHp);
                break;
            case GremlinMerc merc:
            {
                Assert.Equal(1, merc.Creature.GetPower<SurprisePower>()!.Amount);
                ThieveryPower thievery = Assert.IsType<ThieveryPower>(Assert.Single(merc.Creature.Powers.OfType<ThieveryPower>()));
                Assert.Equal(20, thievery.Amount);
                Assert.Same(target, thievery.Target);
                int goldBefore = target.Player!.Gold;
                await merc.PerformMove();
                Assert.Equal(14, targetHpBefore - target.CurrentHp);
                Assert.Equal(Math.Min(20, goldBefore), goldBefore - target.Player.Gold);

                var stageRun = new RunState("underdocks-merc-death", new Overgrowth());
                Player stagePlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), stageRun);
                stageRun.AddPlayer(stagePlayer);
                var stage = new CombatState(stageRun);
                var stageMerc = (GremlinMerc)ModelDb.Monster<GremlinMerc>().MutableClone();
                stage.AddMonster(stageMerc, CombatSide.Enemy, "merc");
                var stageEngine = new CombatEngine(stage);
                await stageEngine.StartCombatAsync();
                CombatState projection = stage.Clone();
                GremlinMerc projectionMerc = Assert.IsType<GremlinMerc>(
                    projection.Enemies.Single(creature => creature.Monster is GremlinMerc).Monster);

                await CreatureCmd.Kill(stageMerc.Creature);
                Creature[] spawned = stage.Enemies
                    .Where(creature => creature.Monster is SneakyGremlin or FatGremlin)
                    .ToArray();
                Assert.Equal(2, spawned.Length);
                Assert.All(spawned, creature =>
                {
                    Assert.True(creature.IsAlive);
                    Assert.True(creature.IsPrimaryEnemy);
                });
                Assert.False(stageEngine.CheckWinCondition());

                await CreatureCmd.Kill(projectionMerc.Creature);
                Creature[] projectedSpawned = projection.Enemies
                    .Where(creature => creature.Monster is SneakyGremlin or FatGremlin)
                    .ToArray();
                Assert.Equal(2, projectedSpawned.Length);
                Assert.All(projectedSpawned, creature =>
                {
                    Assert.True(creature.IsAlive);
                    Assert.True(creature.IsPrimaryEnemy);
                });
                Assert.False(projection.Engine!.CheckWinCondition());
                break;
            }
            case FatGremlin fat:
                Assert.IsType<StunIntent>(Assert.Single(fat.NextMove!.Intents));
                Assert.Equal("FLEE_MOVE", Assert.IsType<MoveState>(fat.MoveStateMachine!.States["SPAWNED_MOVE"]).FollowUpState!.Id);
                break;
            case SneakyGremlin sneaky:
                Assert.IsType<StunIntent>(Assert.Single(sneaky.NextMove!.Intents));
                Assert.Equal("TACKLE_MOVE", Assert.IsType<MoveState>(sneaky.MoveStateMachine!.States["SPAWNED_MOVE"]).FollowUpState!.Id);
                break;
            case HauntedShip ship:
                await ship.PerformMove();
                Assert.Equal(3, target.GetPower<WeakPower>()!.Amount);
                Assert.Equal(5, target.Player!.PlayerCombatState!.DiscardPile.Cards.OfType<Dazed>().Count());
                break;
            case LivingFog fog:
                CombatState clone = state.Clone();
                LivingFog cloneFog = Assert.IsType<LivingFog>(clone.Enemies[0].Monster);
                Assert.Equal(
                    9,
                    cloneFog.NextMove!.Intents.OfType<SingleAttackIntent>().Single().GetSingleDamage(
                        cloneFog.Creature.CombatState!.Allies,
                        cloneFog.Creature));
                await fog.PerformMove();
                Assert.Equal(9, targetHpBefore - target.CurrentHp);
                Assert.Equal(1, target.GetPower<SmoggyPower>()!.Amount);
                break;
            case GasBomb bomb:
            {
                Assert.Equal(1, bomb.Creature.GetPower<MinionPower>()!.Amount);
                Assert.Equal(8, Assert.IsType<DeathBlowIntent>(Assert.Single(bomb.NextMove!.Intents)).GetSingleDamage([target], bomb.Creature));
                await bomb.PerformMove();
                Assert.True(bomb.Creature.IsDead);
                Assert.DoesNotContain(bomb.Creature, state.Enemies);
                Assert.Equal(8, targetHpBefore - target.CurrentHp);
                break;
            }
            case Seapunk seapunk:
                await seapunk.PerformMove();
                Assert.Equal(11, targetHpBefore - target.CurrentHp);
                break;
            case SewerClam clam:
                Assert.Equal(8, clam.Creature.GetPower<PlatingPower>()!.Amount);
                await clam.PerformMove();
                Assert.Equal(10, targetHpBefore - target.CurrentHp);
                break;
            case TwoTailedRat ratMonster:
                await ratMonster.PerformMove();
                Assert.Equal(8, targetHpBefore - target.CurrentHp);
                break;
            case SludgeSpinner sludge:
                await sludge.PerformMove();
                Assert.Equal(8, targetHpBefore - target.CurrentHp);
                Assert.Equal(1, target.GetPower<WeakPower>()!.Amount);
                break;
            case Toadpole toadpoleMonster:
                await toadpoleMonster.PerformMove();
                Assert.Equal(2, toadpoleMonster.Creature.GetPower<ThornsPower>()!.Amount);
                break;
            case PunchConstruct punchMonster:
                Assert.Equal(1, punchMonster.Creature.GetPower<ArtifactPower>()!.Amount);
                await punchMonster.PerformMove();
                Assert.Equal(10, punchMonster.Creature.Block);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(family), family, null);
        }

        Assert.Equal(
            [
                "CorpseSlugsNormal", "CultistsNormal", "FossilStalkerNormal", "GremlinMercNormal",
                "HauntedShipNormal", "LivingFogNormal", "PunchConstructNormal", "SeapunkNormal",
                "SewerClamNormal", "TwoTailedRatsNormal", "CorpseSlugsWeak", "SeapunkWeak",
                "SludgeSpinnerWeak", "ToadpolesWeak",
            ],
            UnderdocksEncounters.BatchA.Concat(UnderdocksEncounters.BatchB).Select(encounter => encounter.Name));
        Assert.Equal(
            [false, false, false, false, false, false, false, false, false, false, true, true, true, true],
            UnderdocksEncounters.BatchA.Concat(UnderdocksEncounters.BatchB).Select(encounter => encounter.IsWeak));
    }
}
