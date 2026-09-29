using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedCardRepairTask1Tests : IDisposable
{
    public GeneratedCardRepairTask1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task CelestialMight_DealsSixPerHit_ThreeOrFourTimesWhenUpgraded()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("task1-celestial-base");
        CelestialMight baseCard = AddToHand<CelestialMight>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await baseCard.PlayAsync(enemy);

        Assert.Equal(hpBefore - 18, enemy.CurrentHp);

        (player, room) = await CreateCombatAsync("task1-celestial-base-strength");
        baseCard = AddToHand<CelestialMight>(player);
        enemy = room.Engine.State.HittableEnemies.Single();
        hpBefore = enemy.CurrentHp;
        await PowerCmd.Apply<StrengthPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);

        await baseCard.PlayAsync(enemy);

        Assert.Equal(hpBefore - 21, enemy.CurrentHp);

        (player, room) = await CreateCombatAsync("task1-celestial-upgrade");
        CelestialMight upgraded = AddToHand<CelestialMight>(player);
        upgraded.Upgrade();
        enemy = room.Engine.State.HittableEnemies.Single();
        hpBefore = enemy.CurrentHp;

        await upgraded.PlayAsync(enemy);

        Assert.Equal(hpBefore - 24, enemy.CurrentHp);

        (player, room) = await CreateCombatAsync("task1-celestial-upgrade-strength");
        upgraded = AddToHand<CelestialMight>(player);
        upgraded.Upgrade();
        enemy = room.Engine.State.HittableEnemies.Single();
        hpBefore = enemy.CurrentHp;
        await PowerCmd.Apply<StrengthPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);

        await upgraded.PlayAsync(enemy);

        Assert.Equal(hpBefore - 28, enemy.CurrentHp);
    }

    [Fact]
    public async Task Conqueror_ForgesThreeOrFive_AndAlwaysAppliesOnePower()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("task1-conqueror-base");
        Conqueror baseCard = AddToHand<Conqueror>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await baseCard.PlayAsync(enemy);

        Assert.Equal(1m, enemy.GetPower<ConquerorPower>()!.Amount);
        SovereignBlade baseBlade = player.PlayerCombatState!.Hand.Cards.OfType<SovereignBlade>().Single();
        int hpBefore = enemy.CurrentHp;
        await baseBlade.PlayAsync(enemy);
        Assert.Equal(hpBefore - 26, enemy.CurrentHp);

        (player, room) = await CreateCombatAsync("task1-conqueror-upgrade");
        Conqueror upgraded = AddToHand<Conqueror>(player);
        upgraded.Upgrade();
        enemy = room.Engine.State.HittableEnemies.Single();

        await upgraded.PlayAsync(enemy);

        Assert.Equal(1m, enemy.GetPower<ConquerorPower>()!.Amount);
        SovereignBlade upgradedBlade = player.PlayerCombatState!.Hand.Cards.OfType<SovereignBlade>().Single();
        hpBefore = enemy.CurrentHp;
        await upgradedBlade.PlayAsync(enemy);
        Assert.Equal(hpBefore - 30, enemy.CurrentHp);
    }

    [Fact]
    public async Task CrushUnder_DamagesForEightOrNine_AndAppliesTemporaryStrengthLoss()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("task1-crush-base", enemyCount: 2);
        CrushUnder baseCard = AddToHand<CrushUnder>(player);
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        int[] hpBefore = enemies.Select(enemy => enemy.CurrentHp).ToArray();

        await baseCard.PlayAsync(target: null);

        for (int index = 0; index < enemies.Length; index++)
        {
            Assert.Equal(hpBefore[index] - 8, enemies[index].CurrentHp);
            Assert.Equal(1m, enemies[index].GetPower<CrushUnderPower>()!.Amount);
            Assert.Equal(-1m, enemies[index].GetPower<StrengthPower>()!.Amount);
        }

        (player, room) = await CreateCombatAsync("task1-crush-upgrade", enemyCount: 2);
        CrushUnder upgraded = AddToHand<CrushUnder>(player);
        upgraded.Upgrade();
        enemies = room.Engine.State.HittableEnemies.ToArray();
        hpBefore = enemies.Select(enemy => enemy.CurrentHp).ToArray();

        await upgraded.PlayAsync(target: null);

        for (int index = 0; index < enemies.Length; index++)
        {
            Assert.Equal(hpBefore[index] - 9, enemies[index].CurrentHp);
            Assert.Equal(2m, enemies[index].GetPower<CrushUnderPower>()!.Amount);
            Assert.Equal(-2m, enemies[index].GetPower<StrengthPower>()!.Amount);
        }
    }

    [Fact]
    public async Task CrushUnder_CapturesEnemiesBeforeAttack_WhenOneDiesFromTheAttack()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("task1-crush-snapshot", enemyCount: 2);
        Creature defeatedEnemy = room.Engine.State.HittableEnemies.First();
        defeatedEnemy.LoseHpInternal(defeatedEnemy.CurrentHp - 8, ValueProp.Unpowered);
        CrushUnder card = AddToHand<CrushUnder>(player);

        await card.PlayAsync(target: null);

        Assert.True(defeatedEnemy.IsDead);
        Assert.Null(defeatedEnemy.CombatState);
        Assert.Null(defeatedEnemy.GetPower<CrushUnderPower>());
    }

    [Fact]
    public async Task Reflect_UpgradeOnlyIncreasesBlock_AndKeepsOnePower()
    {
        (Player player, _) = await CreateCombatAsync("task1-reflect-base");
        Reflect baseCard = AddToHand<Reflect>(player);
        player.PlayerCombatState!.GainStars(3);

        await baseCard.PlayAsync(target: null);

        Assert.Equal(15, player.Creature.Block);
        Assert.Equal(1m, player.Creature.GetPower<ReflectPower>()!.Amount);

        (player, _) = await CreateCombatAsync("task1-reflect-upgrade");
        Reflect upgraded = AddToHand<Reflect>(player);
        upgraded.Upgrade();
        player.PlayerCombatState!.GainStars(3);

        await upgraded.PlayAsync(target: null);

        Assert.Equal(20, player.Creature.Block);
        Assert.Equal(1m, player.Creature.GetPower<ReflectPower>()!.Amount);
    }

    [Fact]
    public async Task Royalties_CannotBeGeneratedInCombat_AndAddsThirtyOrFortyGoldRewards()
    {
        Assert.False(ModelDb.Card<Royalties>().CanBeGeneratedInCombat);

        (Player player, RunState runState, _) = await CreateCombatWithRunAsync("task1-royalties-base");
        Royalties baseCard = AddToHand<Royalties>(player);
        await baseCard.PlayAsync(target: null);
        GoldReward baseReward = Assert.IsType<GoldReward>(Assert.Single(
            RewardsSet.GenerateFor(player, RoomType.Monster, runState).ExtraRewards));
        Assert.Equal(30, baseReward.Amount);

        (player, runState, _) = await CreateCombatWithRunAsync("task1-royalties-upgrade");
        Royalties upgraded = AddToHand<Royalties>(player);
        upgraded.Upgrade();
        await upgraded.PlayAsync(target: null);
        GoldReward upgradedReward = Assert.IsType<GoldReward>(Assert.Single(
            RewardsSet.GenerateFor(player, RoomType.Monster, runState).ExtraRewards));
        Assert.Equal(40, upgradedReward.Amount);
    }

    [Fact]
    public async Task SolarStrike_HasStrikeTag_AndRetainsDamageAndStarsUpgrade()
    {
        Assert.Contains(CardTag.Strike, ModelDb.Card<SolarStrike>().Tags);
        (Player player, CombatRoom room) = await CreateCombatAsync("task1-solar");
        SolarStrike card = AddToHand<SolarStrike>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        int starsBefore = player.PlayerCombatState!.Stars;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 10, enemy.CurrentHp);
        Assert.Equal(starsBefore + 2, player.PlayerCombatState.Stars);
    }

    [Fact]
    public async Task UltimateStrike_HasStrikeTag_AndRetainsDamageUpgrade()
    {
        Assert.Contains(CardTag.Strike, ModelDb.Card<UltimateStrike>().Tags);
        (Player player, CombatRoom room) = await CreateCombatAsync("task1-ultimate-strike");
        UltimateStrike card = AddToHand<UltimateStrike>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 20, enemy.CurrentHp);
    }

    [Fact]
    public async Task UltimateDefend_HasDefendTag_AndRetainsBlockUpgrade()
    {
        Assert.Contains(CardTag.Defend, ModelDb.Card<UltimateDefend>().Tags);
        (Player player, _) = await CreateCombatAsync("task1-ultimate-defend");
        UltimateDefend card = AddToHand<UltimateDefend>(player);
        card.Upgrade();

        await card.PlayAsync(target: null);

        Assert.Equal(15, player.Creature.Block);
    }

    private static TCard AddToHand<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed, int enemyCount = 1)
    {
        (Player player, _, CombatRoom room) = await CreateCombatWithRunAsync(seed, enemyCount);
        return (player, room);
    }

    private static async Task<(Player Player, RunState RunState, CombatRoom Room)> CreateCombatWithRunAsync(
        string seed,
        int enemyCount = 1)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        for (int index = 1; index < enemyCount; index++)
        {
            room.Engine.State.AddMonster(
                (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
                CombatSide.Enemy);
        }
        return (player, runState, room);
    }
}
