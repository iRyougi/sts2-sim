namespace Sts2Sim.Core.Tests.Combat;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public class CombatEngineTests
{
    public CombatEngineTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Powers.MinionPower), typeof(Sts2Sim.Core.Models.Relics.DivineRight), typeof(TrainingDummy), typeof(TwoMoveMonster) });
    }

    private sealed class TestRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        private readonly List<Player> _players = new();

        public TestRunState(string seed) => Rng = new RunRngSet(seed);

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; }

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;

        public void AddPlayer(Player p) => _players.Add(p);
    }

    private static (CombatEngine engine, Player player, Creature dummy) MakeCombat(string seed)
    {
        var runState = new TestRunState(seed);
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var monster = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        Creature dummyCreature = combatState.AddMonster(monster, CombatSide.Enemy);
        var engine = new CombatEngine(combatState);
        return (engine, player, dummyCreature);
    }

    private static (CombatEngine engine, Player player, Creature monster) MakeCombatWithMonster<TMonster>(string seed)
        where TMonster : MonsterModel
    {
        var runState = new TestRunState(seed);
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var monster = (TMonster)ModelDb.Monster<TMonster>().MutableClone();
        Creature monsterCreature = combatState.AddMonster(monster, CombatSide.Enemy);
        var engine = new CombatEngine(combatState);
        return (engine, player, monsterCreature);
    }

    private sealed class TwoMoveMonster : MonsterModel
    {
        public override int MinInitialHp => 20;

        public override int MaxInitialHp => 20;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var moveA = new MoveState("A", _ => Task.CompletedTask, new SingleAttackIntent(1));
            var moveB = new MoveState("B", _ => Task.CompletedTask, new SingleAttackIntent(2));
            moveA.FollowUpState = moveB;
            moveB.FollowUpState = moveA;
            return new MonsterMoveStateMachine(new[] { moveA, moveB }, moveA);
        }
    }

    [Fact]
    public async Task StartCombatAsync_DealsFiveCards_AndSetsEnergyToMax()
    {
        (CombatEngine engine, Player player, _) = MakeCombat("start_combat_test");

        await engine.StartCombatAsync();

        Assert.Equal(5, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(3, player.PlayerCombatState.Energy);
        Assert.True(engine.IsInProgress);
    }

    [Fact]
    public async Task EndPlayerTurnAsync_DiscardsHand_EnemyAttacks_AndStartsFreshPlayerTurn()
    {
        (CombatEngine engine, Player player, _) = MakeCombat("end_turn_test");
        await engine.StartCombatAsync();
        int hpBefore = player.Creature.CurrentHp;

        await engine.EndPlayerTurnAsync();

        Assert.Equal(hpBefore - 8, player.Creature.CurrentHp);
        Assert.Equal(5, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(CombatSide.Player, engine.State.CurrentSide);
        Assert.Equal(2, engine.State.RoundNumber);
    }

    [Fact]
    public async Task PlayCardAsync_StrikeOnDummy_DealsSixDamage()
    {
        (CombatEngine engine, Player player, Creature dummy) = MakeCombat("play_card_test");
        await engine.StartCombatAsync();
        CardModel strike = player.PlayerCombatState!.Hand.Cards.OfType<StrikeRegent>().First();

        await engine.PlayCardAsync(player, strike, dummy);

        Assert.Equal(14, dummy.CurrentHp);
        Assert.Equal(2, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task PlayCardAsync_DoesNotResolveCard_WhenItCannotBePlayed()
    {
        (CombatEngine engine, Player player, Creature dummy) = MakeCombat("unplayable_card_test");
        await engine.StartCombatAsync();
        CardModel strike = player.PlayerCombatState!.Hand.Cards.OfType<StrikeRegent>().First();
        player.PlayerCombatState.Energy = 0;

        await engine.PlayCardAsync(player, strike, dummy);

        Assert.Equal(20, dummy.CurrentHp);
        Assert.Equal(0, player.PlayerCombatState.Energy);
        Assert.Contains(strike, player.PlayerCombatState.Hand.Cards);
        Assert.DoesNotContain(strike, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task StartTurnAsync_RollsNextEnemyMove_OnceNewPlayerTurnBegins()
    {
        // 怪物下一招的滚动时机在玩家回合开始、Hook.BeforeSideTurnStart 之后（见 CombatEngine.StartTurnAsync
        // 的注释）,而不是敌方回合结束时——这样玩家在自己回合看到的意图,和敌方回合真正执行的招式才会一致。
        (CombatEngine engine, _, Creature monster) = MakeCombatWithMonster<TwoMoveMonster>("visible_intent_test");
        await engine.StartCombatAsync();

        Assert.Equal("A", monster.Monster!.NextMove!.Id);

        await engine.EndPlayerTurnAsync();

        Assert.Equal(CombatSide.Player, engine.State.CurrentSide);
        Assert.Equal(2, engine.State.RoundNumber);
        Assert.Equal("B", monster.Monster!.NextMove!.Id);
    }

    [Fact]
    public async Task FullCombat_RepeatedlyStrikingTheDummy_EventuallyWinsCombat()
    {
        (CombatEngine engine, Player player, Creature dummy) = MakeCombat("full_combat_test");
        await engine.StartCombatAsync();

        for (int round = 0; round < 10 && engine.IsInProgress; round++)
        {
            foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.OfType<StrikeRegent>().ToList())
            {
                if (!engine.IsInProgress)
                {
                    break;
                }

                await engine.PlayCardAsync(player, card, dummy);
                if (engine.CheckWinCondition())
                {
                    break;
                }
            }

            if (engine.IsInProgress)
            {
                await engine.EndPlayerTurnAsync();
            }
        }

        Assert.False(engine.IsInProgress);
        Assert.True(engine.Won);
        Assert.Equal(0, dummy.CurrentHp);
    }

    [Fact]
    public async Task CheckWinCondition_DetectsDefeat_WhenPlayerDies()
    {
        (CombatEngine engine, Player player, _) = MakeCombat("defeat_test");
        await engine.StartCombatAsync();
        player.Creature.LoseHpInternal(999m, ValueProp.Move);

        bool ended = engine.CheckWinCondition();

        Assert.True(ended);
        Assert.False(engine.IsInProgress);
        Assert.False(engine.Won);
    }
    [Fact]
    public async Task CheckWinCondition_WinsWhenOnlyLivingEnemyIsSecondary()
    {
        (CombatEngine engine, _, Creature secondary) = MakeCombat("secondary_enemy_win_test");
        await engine.StartCombatAsync();
        await Sts2Sim.Core.Commands.PowerCmd.Apply<Sts2Sim.Core.Models.Powers.MinionPower>(
            engine.State,
            secondary,
            1m,
            secondary,
            cardSource: null);

        bool ended = engine.CheckWinCondition();

        Assert.True(secondary.IsAlive);
        Assert.True(secondary.IsSecondaryEnemy);
        Assert.False(secondary.IsPrimaryEnemy);
        Assert.True(ended);
        Assert.True(engine.Won);
    }
}
