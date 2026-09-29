using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedCardRepairTask2Tests : IDisposable
{
    public GeneratedCardRepairTask2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(Task2AlwaysUnhittablePower),
            typeof(Task2UnhittableAfterDamagePower),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public async Task Constellation_RoutesDrawEnergyAndBlockToSelectedAlly(
        bool upgraded,
        int expectedBlock)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task2-constellation-{upgraded}",
            playerCount: 2,
            enemyCount: 1);
        Player owner = players[0];
        Player ally = players[1];
        ClearHand(owner);
        ClearHand(ally);
        Constellation card = AddToHand<Constellation>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }
        owner.PlayerCombatState!.Energy = 0;
        ally.PlayerCombatState!.Energy = 0;
        int allyDrawBefore = ally.PlayerCombatState.DrawPile.Cards.Count;

        await room.Engine.PlayCardAsync(owner, card, ally.Creature);

        Assert.Empty(owner.PlayerCombatState.Hand.Cards);
        Assert.Single(ally.PlayerCombatState.Hand.Cards);
        Assert.Equal(allyDrawBefore - 1, ally.PlayerCombatState.DrawPile.Cards.Count);
        Assert.Equal(0, owner.PlayerCombatState.Energy);
        Assert.Equal(1, ally.PlayerCombatState.Energy);
        Assert.Equal(0, owner.Creature.Block);
        Assert.Equal(expectedBlock, ally.Creature.Block);
    }

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 13)]
    public async Task Intercept_BlocksOwnerAndCoversSelectedAllyUntilEnemyTurnEnds(
        bool upgraded,
        int expectedBlock)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task2-intercept-{upgraded}",
            playerCount: 2,
            enemyCount: 1);
        Player owner = players[0];
        Player ally = players[1];
        Creature enemy = room.Engine.State.Enemies.Single();
        Intercept card = AddToHand<Intercept>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, card, ally.Creature);

        Assert.Equal(expectedBlock, owner.Creature.Block);
        Assert.Equal(0, ally.Creature.Block);
        AssertCovered(ally.Creature);
        int allyHpBefore = ally.Creature.CurrentHp;
        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { ally.Creature },
            10m,
            ValueProp.Move,
            enemy,
            cardSource: null,
            cardPlay: null);
        Assert.Equal(allyHpBefore, ally.Creature.CurrentHp);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, room.Engine.State.Enemies);

        AssertNotCovered(ally.Creature);
        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { ally.Creature },
            10m,
            ValueProp.Move,
            enemy,
            cardSource: null,
            cardPlay: null);
        Assert.Equal(allyHpBefore - 10, ally.Creature.CurrentHp);
    }

    [Fact]
    public async Task Intercept_CoveredPowerIsRemovedWhenItsApplierDies()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            "task2-intercept-applier-death",
            playerCount: 2,
            enemyCount: 1);
        Player owner = players[0];
        Player ally = players[1];
        Creature enemy = room.Engine.State.Enemies.Single();
        Intercept card = AddToHand<Intercept>(owner);
        await room.Engine.PlayCardAsync(owner, card, ally.Creature);
        AssertCovered(ally.Creature);

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { owner.Creature },
            999m,
            ValueProp.Unblockable,
            enemy,
            cardSource: null,
            cardPlay: null);

        Assert.True(owner.Creature.IsDead);
        AssertNotCovered(ally.Creature);
    }

    [Theory]
    [InlineData(false, 14)]
    [InlineData(true, 21)]
    public async Task MeteorShower_DebuffsOnlyEnemiesStillHittableAfterDamage(
        bool upgraded,
        int expectedDamage)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task2-meteor-{upgraded}",
            playerCount: 1,
            enemyCount: 3);
        Player owner = players[0];
        Creature defeated = room.Engine.State.Enemies[0];
        Creature becameUnhittable = room.Engine.State.Enemies[1];
        Creature hittable = room.Engine.State.Enemies[2];
        defeated.LoseHpInternal(defeated.CurrentHp - 1m, ValueProp.Unblockable);
        await PowerCmd.Apply<Task2UnhittableAfterDamagePower>(
            room.Engine.State,
            becameUnhittable,
            1m,
            owner.Creature,
            cardSource: null);
        int unhittableHpBefore = becameUnhittable.CurrentHp;
        int hittableHpBefore = hittable.CurrentHp;
        MeteorShower card = AddToHand<MeteorShower>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, card, target: null);

        Assert.True(defeated.IsDead);
        Assert.Equal(unhittableHpBefore - expectedDamage, becameUnhittable.CurrentHp);
        Assert.Equal(hittableHpBefore - expectedDamage, hittable.CurrentHp);
        Assert.DoesNotContain(becameUnhittable, room.Engine.State.HittableEnemies);
        AssertNoWeakOrVulnerable(defeated);
        AssertNoWeakOrVulnerable(becameUnhittable);
        Assert.Equal(2, Assert.Single(hittable.Powers.OfType<WeakPower>()).Amount);
        Assert.Equal(2, Assert.Single(hittable.Powers.OfType<VulnerablePower>()).Amount);
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 11)]
    public async Task Omnislice_SplashesOnlyHittableTargetTeammates(
        bool upgraded,
        int expectedDamage)
    {
        var observer = new Task2DamageObserver();
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task2-omnislice-{upgraded}",
            playerCount: 1,
            enemyCount: 3,
            observer);
        Player owner = players[0];
        Creature target = room.Engine.State.Enemies[0];
        Creature unhittableTeammate = room.Engine.State.Enemies[1];
        Creature hittableTeammate = room.Engine.State.Enemies[2];
        await PowerCmd.Apply<Task2AlwaysUnhittablePower>(
            room.Engine.State,
            unhittableTeammate,
            1m,
            owner.Creature,
            cardSource: null);
        await PowerCmd.Apply<SkittishPower>(
            room.Engine.State,
            hittableTeammate,
            7m,
            hittableTeammate,
            cardSource: null);
        observer.Results.Clear();
        int targetHpBefore = target.CurrentHp;
        int unhittableHpBefore = unhittableTeammate.CurrentHp;
        int hittableHpBefore = hittableTeammate.CurrentHp;
        Omnislice card = AddToHand<Omnislice>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, card, target);

        Assert.Equal(targetHpBefore - expectedDamage, target.CurrentHp);
        Assert.Equal(unhittableHpBefore, unhittableTeammate.CurrentHp);
        Assert.Equal(hittableHpBefore - expectedDamage, hittableTeammate.CurrentHp);
        Assert.Equal(7, hittableTeammate.Block);
        Assert.DoesNotContain(unhittableTeammate, room.Engine.State.HittableEnemies);
        Assert.Collection(
            observer.Results,
            result => Assert.Same(target, result.Receiver),
            result => Assert.Same(hittableTeammate, result.Receiver));
    }

    [Theory]
    [InlineData(false, 12)]
    [InlineData(true, 17)]
    public async Task Rally_BlocksOnlyLivingPlayerAllies(bool upgraded, int expectedBlock)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task2-rally-{upgraded}",
            playerCount: 3,
            enemyCount: 1);
        Player owner = players[0];
        Player livingAlly = players[1];
        Player deadAlly = players[2];
        deadAlly.Creature.LoseHpInternal(deadAlly.Creature.CurrentHp, ValueProp.Unblockable);
        Creature nonPlayerAlly = room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            CombatSide.Player);
        Rally card = AddToHand<Rally>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, card, target: null);

        Assert.Equal(expectedBlock, owner.Creature.Block);
        Assert.Equal(expectedBlock, livingAlly.Creature.Block);
        Assert.Equal(0, deadAlly.Creature.Block);
        Assert.Equal(0, nonPlayerAlly.Block);
    }

    [Theory]
    [InlineData(false, 16)]
    [InlineData(true, 19)]
    public async Task SpoilsOfBattle_ForgesBeforeDrawing(bool upgraded, int expectedBladeDamage)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task2-spoils-{upgraded}",
            playerCount: 1,
            enemyCount: 1);
        Player owner = players[0];
        SpoilsOfBattle card = AddToHand<SpoilsOfBattle>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }
        while (owner.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            AddToHand<DefendRegent>(owner);
        }
        int drawBefore = owner.PlayerCombatState.DrawPile.Cards.Count;

        await room.Engine.PlayCardAsync(owner, card, target: null);

        SovereignBlade blade = Assert.Single(owner.PlayerCombatState.Hand.Cards.OfType<SovereignBlade>());
        Assert.Equal(drawBefore, owner.PlayerCombatState.DrawPile.Cards.Count);
        Assert.DoesNotContain(owner.PlayerCombatState.DiscardPile.Cards, candidate => candidate is SovereignBlade);
        Creature enemy = room.Engine.State.Enemies.Single();
        int enemyHpBefore = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(owner, blade, enemy);
        Assert.Equal(enemyHpBefore - expectedBladeDamage, enemy.CurrentHp);
    }

    private static void AssertCovered(Creature creature) =>
        Assert.Single(creature.Powers, power => power.GetType().Name == "CoveredPower");

    private static void AssertNotCovered(Creature creature) =>
        Assert.DoesNotContain(creature.Powers, power => power.GetType().Name == "CoveredPower");

    private static void AssertNoWeakOrVulnerable(Creature creature)
    {
        Assert.DoesNotContain(creature.Powers, power => power is WeakPower);
        Assert.DoesNotContain(creature.Powers, power => power is VulnerablePower);
    }

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player[] Players, CombatRoom Room)> CreateCombatAsync(
        string seed,
        int playerCount,
        int enemyCount,
        ICombatObserver? observer = null)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player[] players = Enumerable.Range(0, playerCount)
            .Select(_ => Player.CreateForNewRun(ModelDb.Character<Regent>(), runState))
            .ToArray();
        foreach (Player player in players)
        {
            runState.AddPlayer(player);
        }

        var room = new CombatRoom((Func<IReadOnlyList<MonsterModel>>)(() =>
            Enumerable.Range(0, enemyCount)
                .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone())
                .ToArray()));
        if (observer is not null)
        {
            room.ConfigureObserver(observer);
        }
        await room.Enter(runState);
        return (players, room);
    }
}

file sealed class Task2AlwaysUnhittablePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldAllowHitting(Creature creature) => creature != Owner;
}

file sealed class Task2UnhittableAfterDamagePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldAllowHitting(Creature creature) =>
        creature != Owner || Owner.CurrentHp == Owner.MaxHp;
}

file sealed class Task2DamageObserver : ICombatObserver
{
    public List<DamageResult> Results { get; } = [];

    public void CombatStarted(CombatState state) { }

    public void PlayerTurnStarted(CombatState state) { }

    public void CardDrawn(CardModel card) { }

    public void CardPlayStarted(CardModel card, Creature? target) { }

    public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay) { }

    public void EnemyMoveStarted(Creature source, string moveId) { }

    public void EnemyMoveFinished(Creature source, string moveId) { }

    public void DamageResolved(Creature? dealer, DamageResult result) => Results.Add(result);

    public void PlayerTurnEnded(CombatState state) { }

    public void PotionUseStarted(PotionModel potion, Creature? target) { }

    public void PotionUseFinished(PotionModel potion, Creature? target) { }
}
