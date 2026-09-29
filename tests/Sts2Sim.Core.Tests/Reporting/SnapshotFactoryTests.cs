using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Reporting;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Reporting;

[Collection("ModelDb")]
public sealed class SnapshotFactoryTests : IDisposable
{
    public SnapshotFactoryTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
        [
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(WeakPower),
            typeof(VulnerablePower),
            typeof(StrengthPower),
            typeof(DivineRight),
            typeof(Lantern),
            typeof(StrengthPotion),
            typeof(TrainingDummy),
        ]);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void SnapshotMethods_CaptureConstructedPlayerAndEnemyState()
    {
        var runState = new SnapshotRunState();
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        player.ResetCombatState();
        player.PopulateCombatState(runState.Rng.Shuffle);
        player.PlayerCombatState!.Energy = 2;
        player.PlayerCombatState.GainStars(3m);
        player.Creature.GainBlockInternal(4m);

        player.PlayerCombatState.Hand.AddInternal(FindCard<StrikeRegent>(player.PlayerCombatState.DrawPile));
        player.PlayerCombatState.Hand.AddInternal(FindCard<DefendRegent>(player.PlayerCombatState.DrawPile));
        player.PlayerCombatState.DiscardPile.AddInternal(FindCard<StrikeRegent>(player.PlayerCombatState.DrawPile));
        player.PlayerCombatState.ExhaustPile.AddInternal(FindCard<DefendRegent>(player.PlayerCombatState.DrawPile));

        var playerStrength = (StrengthPower)ModelDb.Power<StrengthPower>().MutableClone();
        playerStrength.ApplyInternal(player.Creature, 3m);

        var lantern = (Lantern)ModelDb.Relic<Lantern>().MutableClone();
        lantern.AssignOwner(player);
        player.AddRelicInternal(lantern);
        player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());

        var monster = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        Creature enemy = combatState.AddMonster(monster, CombatSide.Enemy, "front");
        enemy.GainBlockInternal(7m);
        var enemyWeak = (WeakPower)ModelDb.Power<WeakPower>().MutableClone();
        enemyWeak.ApplyInternal(enemy, 2m);
        monster.SetUpForCombat();
        monster.RollMove(combatState.Allies);

        PlayerSnapshot playerSnapshot = SnapshotFactory.SnapshotPlayer(player);
        EnemySnapshot enemySnapshot = SnapshotFactory.SnapshotEnemy(enemy);

        Assert.Equal(75, playerSnapshot.Hp);
        Assert.Equal(75, playerSnapshot.MaxHp);
        Assert.Equal(4, playerSnapshot.Block);
        Assert.Equal(2, playerSnapshot.Energy);
        Assert.Equal(3, playerSnapshot.Stars);
        Assert.Equal(["CARD.STRIKE_REGENT", "CARD.DEFEND_REGENT"], playerSnapshot.Hand);
        Assert.Equal(6, playerSnapshot.DrawPileSize);
        Assert.Equal(1, playerSnapshot.DiscardPileSize);
        Assert.Equal(1, playerSnapshot.ExhaustPileSize);
        PowerSnapshot playerPower = Assert.Single(playerSnapshot.Powers);
        Assert.Equal("POWER.STRENGTH_POWER", playerPower.Id);
        Assert.Equal(3, playerPower.Amount);

        Assert.Equal(
        [
            "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT", "CARD.STRIKE_REGENT",
            "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT", "CARD.DEFEND_REGENT",
            "CARD.FALLING_STAR", "CARD.VENERATE",
        ],
        SnapshotFactory.SnapshotDeck(player));
        Assert.Equal(["RELIC.DIVINE_RIGHT", "RELIC.LANTERN"], SnapshotFactory.SnapshotRelics(player));
        Assert.Equal(["POTION.STRENGTH_POTION", null, null], SnapshotFactory.SnapshotPotions(player));

        Assert.Equal("front", enemySnapshot.Slot);
        Assert.Equal("MONSTER.TRAINING_DUMMY", enemySnapshot.Id);
        Assert.Equal(20, enemySnapshot.Hp);
        Assert.Equal(20, enemySnapshot.MaxHp);
        Assert.Equal(7, enemySnapshot.Block);
        PowerSnapshot enemyPower = Assert.Single(enemySnapshot.Powers);
        Assert.Equal("POWER.WEAK_POWER", enemyPower.Id);
        Assert.Equal(2, enemyPower.Amount);
        Assert.Equal("Attack 8", enemySnapshot.Intent);
    }

    private static CardModel FindCard<TCard>(Sts2Sim.Core.Entities.Cards.CardPile pile)
        where TCard : CardModel => pile.Cards.OfType<TCard>().First();

    private sealed class SnapshotRunState : IRunState
    {
        private readonly List<Player> _players = [];

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;

        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;

        public RunRngSet Rng { get; } = new("snapshot-factory");

        public void AddPlayer(Player player) => _players.Add(player);

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();
    }
}
