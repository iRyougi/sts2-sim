namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class UnderdocksEliteTests : IDisposable
{
    public UnderdocksEliteTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("phantasmal", 0, 26, 31, 5, 7, 1)]
    [InlineData("skulking", (int)AscensionLevel.DeadlyEnemies, 80, 80, 16, 11, 8)]
    [InlineData("terror", (int)AscensionLevel.DeadlyEnemies, 150, 150, 18, 4, 4)]
    public async Task EliteFamilies_UseAuthoritativeHpMovesAndSignatureMechanic(
        string family,
        int ascension,
        int expectedMinHp,
        int expectedMaxHp,
        int expectedFirstAttack,
        int expectedSecondAttack,
        int expectedRepeatDamage)
    {
        switch (family)
        {
            case "phantasmal":
            {
                (CombatState state, IReadOnlyList<Creature> targets, Player player) = CreateCombat(ascension);
                PhantasmalGardener[] gardeners = [
                    AddMonster<PhantasmalGardener>(state, "first"),
                    AddMonster<PhantasmalGardener>(state, "second"),
                    AddMonster<PhantasmalGardener>(state, "third"),
                    AddMonster<PhantasmalGardener>(state, "fourth"),
                ];

                Assert.Equal(expectedMinHp, gardeners[0].MinInitialHp);
                Assert.Equal(expectedMaxHp, gardeners[0].MaxInitialHp);
                Assert.Equal("FLAIL_MOVE", gardeners[0].NextMove!.StateId);
                Assert.Equal("BITE_MOVE", gardeners[1].NextMove!.StateId);
                Assert.Equal("LASH_MOVE", gardeners[2].NextMove!.StateId);
                Assert.Equal("ENLARGE_MOVE", gardeners[3].NextMove!.StateId);
                Assert.Equal(6, gardeners[0].Creature.GetPower<SkittishPower>()!.Amount);
                Assert.Equal(expectedRepeatDamage,
                    Assert.IsType<MultiAttackIntent>(Assert.Single(gardeners[0].NextMove!.Intents))
                        .GetSingleDamage(targets, gardeners[0].Creature));
                await gardeners[3].PerformMove();
                Assert.Equal(2, gardeners[3].Creature.GetPower<StrengthPower>()!.Amount);

                CardModel card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
                card.AssignOwner(player);
                await DamageCmd.Attack(1).FromCard(card, null).Targeting(gardeners[0].Creature).Execute();
                Assert.Equal(6, gardeners[0].Creature.Block);
                await DamageCmd.Attack(10).FromCard(card, null).Targeting(gardeners[0].Creature).Execute();
                Assert.Equal(0, gardeners[0].Creature.Block);
                await Hook.AfterSideTurnEnd(state, CombatSide.Player, state.Allies);
                await DamageCmd.Attack(1).FromCard(card, null).Targeting(gardeners[0].Creature).Execute();
                Assert.Equal(6, gardeners[0].Creature.Block);
                break;
            }
            case "skulking":
            {
                (CombatState state, IReadOnlyList<Creature> targets, _) = CreateCombat(ascension);
                SkulkingColony colony = AddMonster<SkulkingColony>(state);
                Assert.Equal(expectedMinHp, colony.MinInitialHp);
                Assert.Equal(expectedMaxHp, colony.MaxInitialHp);
                Assert.Equal(20, colony.Creature.GetPower<HardenedShellPower>()!.Amount);
                Assert.Equal("ZOOM_MOVE", colony.NextMove!.StateId);
                Assert.Equal(expectedFirstAttack,
                    Assert.IsType<SingleAttackIntent>(Assert.Single(colony.NextMove.Intents))
                        .GetSingleDamage(targets, colony.Creature));
                Assert.Equal(expectedFirstAttack,
                    Assert.IsType<MoveState>(colony.MoveStateMachine!.States["ZOOM_MOVE_2"])
                        .Intents.OfType<SingleAttackIntent>().Single()
                        .GetSingleDamage(targets, colony.Creature));

                CombatState colonyCloneState = state.Clone();
                SkulkingColony colonyClone = Assert.IsType<SkulkingColony>(
                    Assert.Single(colonyCloneState.Enemies).Monster);
                Assert.Equal(expectedFirstAttack,
                    Assert.IsType<MoveState>(colonyClone.MoveStateMachine!.States["ZOOM_MOVE"])
                        .Intents.OfType<SingleAttackIntent>().Single()
                        .GetSingleDamage(colonyCloneState.Allies, colonyClone.Creature));
                Assert.Equal(expectedFirstAttack,
                    Assert.IsType<MoveState>(colonyClone.MoveStateMachine.States["ZOOM_MOVE_2"])
                        .Intents.OfType<SingleAttackIntent>().Single()
                        .GetSingleDamage(colonyCloneState.Allies, colonyClone.Creature));
                Assert.Equal(expectedSecondAttack,
                    Assert.IsType<MoveState>(colonyClone.MoveStateMachine.States["INERTIA_MOVE"])
                        .Intents.OfType<SingleAttackIntent>().Single()
                        .GetSingleDamage(colonyCloneState.Allies, colonyClone.Creature));
                MultiAttackIntent clonedPiercing = Assert.IsType<MoveState>(
                        colonyClone.MoveStateMachine.States["PIERCING_STABS_MOVE"])
                    .Intents.OfType<MultiAttackIntent>().Single();
                Assert.Equal(expectedRepeatDamage,
                    clonedPiercing.GetSingleDamage(colonyCloneState.Allies, colonyClone.Creature));
                Assert.Equal(2, clonedPiercing.Repeats);

                colony.MoveStateMachine!.OnMovePerformed(colony.NextMove);
                colony.RollMove(targets);
                Assert.Equal("ZOOM_MOVE_2", colony.NextMove!.StateId);
                colony.MoveStateMachine.OnMovePerformed(colony.NextMove);
                colony.RollMove(targets);
                Assert.Equal("INERTIA_MOVE", colony.NextMove!.StateId);
                Assert.Equal(expectedSecondAttack,
                    Assert.IsType<SingleAttackIntent>(colony.NextMove.Intents.First(intent => intent is SingleAttackIntent))
                        .GetSingleDamage(targets, colony.Creature));
                colony.MoveStateMachine.OnMovePerformed(colony.NextMove);
                colony.RollMove(targets);
                Assert.Equal("PIERCING_STABS_MOVE", colony.NextMove!.StateId);
                Assert.Equal(expectedRepeatDamage,
                    Assert.IsType<MultiAttackIntent>(Assert.Single(colony.NextMove.Intents))
                        .GetSingleDamage(targets, colony.Creature));

                colony.SetMoveImmediate(
                    (MoveState)colony.MoveStateMachine!.States["INERTIA_MOVE"],
                    forceTransition: true);
                await colony.PerformMove();
                Assert.Equal(4, colony.Creature.GetPower<StrengthPower>()!.Amount);
                HardenedShellPower shell = colony.Creature.GetPower<HardenedShellPower>()!;
                int hpBefore = colony.Creature.CurrentHp;
                await CreatureCmd.Damage(state, [colony.Creature], 12m, ValueProp.Move, targets[0], null, null);
                await CreatureCmd.Damage(state, [colony.Creature], 12m, ValueProp.Move, targets[0], null, null);
                Assert.Equal(20, hpBefore - colony.Creature.CurrentHp);
                Assert.Equal(0, shell.DisplayAmount);
                await Hook.BeforeSideTurnStart(state, CombatSide.Enemy, state.Enemies);
                await CreatureCmd.Damage(state, [colony.Creature], 1m, ValueProp.Move, targets[0], null, null);
                Assert.Equal(19, shell.DisplayAmount);
                break;
            }
            case "terror":
            {
                (CombatState state, IReadOnlyList<Creature> targets, _) = CreateCombat(ascension);
                TerrorEel eel = AddMonster<TerrorEel>(state);
                Assert.Equal(expectedMinHp, eel.MinInitialHp);
                Assert.Equal(expectedMaxHp, eel.MaxInitialHp);
                Assert.Equal(75, eel.Creature.GetPower<ShriekPower>()!.Amount);
                Assert.Equal("CRASH_MOVE", eel.NextMove!.StateId);
                Assert.Equal(expectedFirstAttack,
                    Assert.IsType<SingleAttackIntent>(Assert.Single(eel.NextMove.Intents))
                        .GetSingleDamage(targets, eel.Creature));
                eel.MoveStateMachine!.OnMovePerformed(eel.NextMove);
                eel.RollMove(targets);
                Assert.Equal("THRASH_MOVE", eel.NextMove!.StateId);
                Assert.Equal(expectedRepeatDamage,
                    Assert.IsType<MultiAttackIntent>(eel.NextMove.Intents.OfType<MultiAttackIntent>().Single())
                        .GetSingleDamage(targets, eel.Creature));
                Assert.Equal(3, eel.NextMove.Intents.OfType<MultiAttackIntent>().Single().Repeats);

                CombatState eelCloneState = state.Clone();
                TerrorEel eelClone = Assert.IsType<TerrorEel>(Assert.Single(eelCloneState.Enemies).Monster);
                Assert.Equal(expectedFirstAttack,
                    Assert.IsType<MoveState>(eelClone.MoveStateMachine!.States["CRASH_MOVE"])
                        .Intents.OfType<SingleAttackIntent>().Single()
                        .GetSingleDamage(eelCloneState.Allies, eelClone.Creature));
                MultiAttackIntent clonedThrash = Assert.IsType<MoveState>(
                        eelClone.MoveStateMachine.States["THRASH_MOVE"])
                    .Intents.OfType<MultiAttackIntent>().Single();
                Assert.Equal(expectedRepeatDamage,
                    clonedThrash.GetSingleDamage(eelCloneState.Allies, eelClone.Creature));
                Assert.Equal(3, clonedThrash.Repeats);

                await eel.PerformMove();
                Assert.Equal(6, eel.Creature.GetPower<VigorPower>()!.Amount);
                eel.SetMoveImmediate(eel.TerrorState, forceTransition: true);
                await eel.PerformMove();
                Assert.Equal(99, targets[0].GetPower<VulnerablePower>()!.Amount);
                await CreatureCmd.Damage(state, [eel.Creature], 76m, ValueProp.Move, targets[0], null, null);
                Assert.Null(eel.Creature.GetPower<ShriekPower>());
                Assert.Equal("STUNNED", eel.NextMove!.StateId);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(family), family, null);
        }
    }

    private static (CombatState State, IReadOnlyList<Creature> Targets, Player Player) CreateCombat(int ascension)
    {
        var run = new RunState($"underdocks-elite-{ascension}", new Overgrowth(), ascension);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        player.ResetCombatState();
        var state = new CombatState(run);
        state.AddPlayerCreature(player.Creature);
        return (state, [player.Creature], player);
    }

    private static T AddMonster<T>(CombatState state, string? slot = null)
        where T : MonsterModel
    {
        var monster = (T)ModelDb.Monster<T>().MutableClone();
        state.AddMonster(monster, CombatSide.Enemy, slot);
        monster.SetUpForCombat();
        monster.AfterAddedToRoom().GetAwaiter().GetResult();
        monster.RollMove(state.Allies);
        return monster;
    }
}
