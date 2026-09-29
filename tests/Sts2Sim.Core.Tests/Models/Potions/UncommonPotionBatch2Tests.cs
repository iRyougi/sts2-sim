using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
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

namespace Sts2Sim.Core.Tests.Models.Potions;

[Collection("ModelDb")]
public sealed class UncommonPotionBatch2Tests : IDisposable
{
    public UncommonPotionBatch2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(UncommonPotionBatch2TestCharacter)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<string, TargetType> MetadataCases => new()
    {
        { "KingsCourage", TargetType.AnyPlayer },
        { "LiquidBronze", TargetType.AnyPlayer },
        { "PotionOfBinding", TargetType.AllEnemies },
        { "PowderedDemise", TargetType.AnyEnemy },
        { "RadiantTincture", TargetType.AnyPlayer },
    };

    [Theory]
    [MemberData(nameof(MetadataCases))]
    public void Metadata_IsExactlyUncommonCombatOnlyWithExpectedTarget(
        string potionName,
        TargetType expectedTarget)
    {
        PotionModel potion = GetCanonicalPotion(potionName);

        Assert.Equal(PotionRarity.Uncommon, potion.Rarity);
        Assert.Equal(PotionUsage.CombatOnly, potion.Usage);
        Assert.Equal(expectedTarget, potion.TargetType);
    }

    [Fact]
    public async Task KingsCourage_ForgesExactlyFifteenForTheTargetPlayer()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("kings-courage", playerCount: 2);
        Player potionOwner = players[0];
        Player targetPlayer = players[1];

        await UsePotionAsync("KingsCourage", potionOwner, targetPlayer.Creature);

        Assert.Empty(AllSovereignBlades(potionOwner));
        SovereignBlade blade = Assert.Single(AllSovereignBlades(targetPlayer));
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        targetPlayer.PlayerCombatState!.Energy = 3;

        await room.Engine.PlayCardAsync(targetPlayer, blade, enemy);

        Assert.Equal(hpBefore - 25, enemy.CurrentHp);
    }

    [Fact]
    public async Task LiquidBronze_AppliesExactlyThreeThornsToTheTargetPlayer()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("liquid-bronze", playerCount: 2);
        Player potionOwner = players[0];
        Player targetPlayer = players[1];
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int targetHpBefore = targetPlayer.Creature.CurrentHp;
        int enemyHpBefore = enemy.CurrentHp;

        await UsePotionAsync("LiquidBronze", potionOwner, targetPlayer.Creature);

        ThornsPower thorns = Assert.Single(targetPlayer.Creature.Powers.OfType<ThornsPower>());
        Assert.Equal(3, thorns.Amount);
        Assert.Empty(potionOwner.Creature.Powers.OfType<ThornsPower>());

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { targetPlayer.Creature },
            1m,
            ValueProp.Move,
            enemy,
            cardSource: null,
            cardPlay: null);

        Assert.Equal(targetHpBefore - 1, targetPlayer.Creature.CurrentHp);
        Assert.Equal(enemyHpBefore - 3, enemy.CurrentHp);
        Assert.Equal(3, thorns.Amount);
    }

    [Fact]
    public async Task PotionOfBinding_IgnoresPassedTarget_AndAppliesBothDebuffsToAllHittableEnemies()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("potion-of-binding");
        Player player = players[0];
        Creature firstLiving = room.Engine.State.HittableEnemies.Single();
        Creature secondLiving = AddEnemy(room);
        Creature deadEnemy = AddEnemy(room);
        deadEnemy.LoseHpInternal(deadEnemy.CurrentHp, ValueProp.Unpowered);
        PotionModel potion = (PotionModel)GetCanonicalPotion("PotionOfBinding").MutableClone();
        potion.AssignOwner(player);

        await potion.UseInternal(player.Creature);

        foreach (Creature enemy in new[] { firstLiving, secondLiving })
        {
            Assert.Equal(1, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount);
            Assert.Equal(1, Assert.Single(enemy.Powers.OfType<VulnerablePower>()).Amount);
        }

        Assert.Empty(deadEnemy.Powers);
        Assert.Empty(player.Creature.Powers);
    }

    [Fact]
    public async Task PowderedDemise_DealsPersistentNineUnblockableUnpoweredDamageOnlyAtOwnerSideEnd()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("powdered-demise");
        Player player = players[0];
        Creature owner = room.Engine.State.HittableEnemies.Single();
        Creature otherEnemy = AddEnemy(room);
        await PowerCmd.Apply<VulnerablePower>(
            room.Engine.State,
            owner,
            99m,
            player.Creature,
            cardSource: null);
        owner.GainBlockInternal(100m);

        await UsePotionAsync("PowderedDemise", player, owner);

        PowerModel demise = Assert.Single(
            owner.Powers,
            power => power.GetType() == RequireTask8Type("DemisePower"));
        Assert.Equal(PowerType.Debuff, demise.Type);
        Assert.Equal(PowerStackType.Counter, demise.StackType);
        Assert.Equal(9, demise.Amount);
        int hpBefore = owner.CurrentHp;

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            new[] { otherEnemy });

        Assert.Equal(hpBefore, owner.CurrentHp);
        Assert.Equal(100, owner.Block);
        Assert.Equal(9, demise.Amount);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            new[] { owner });
        Assert.Equal(hpBefore - 9, owner.CurrentHp);
        Assert.Equal(100, owner.Block);
        Assert.Equal(9, demise.Amount);
        Assert.Contains(demise, owner.Powers);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            new[] { owner });
        Assert.Equal(hpBefore - 18, owner.CurrentHp);
        Assert.Equal(100, owner.Block);
        Assert.Equal(9, demise.Amount);
        Assert.Contains(demise, owner.Powers);
    }

    [Fact]
    public async Task RadiantTincture_GainsOneImmediately_ThenOneOnThreeOwnerResetsAndDecays()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("radiant-tincture", playerCount: 2);
        Player potionOwner = players[0];
        Player targetPlayer = players[1];
        potionOwner.PlayerCombatState!.Energy = 7;
        targetPlayer.PlayerCombatState!.Energy = 0;

        await UsePotionAsync("RadiantTincture", potionOwner, targetPlayer.Creature);

        Assert.Equal(1, targetPlayer.PlayerCombatState.Energy);
        PowerModel radiance = Assert.Single(
            targetPlayer.Creature.Powers,
            power => power.GetType() == RequireTask8Type("RadiancePower"));
        Assert.Equal(PowerType.Buff, radiance.Type);
        Assert.Equal(PowerStackType.Counter, radiance.StackType);
        Assert.Equal(3, radiance.Amount);

        await Hook.AfterEnergyReset(room.Engine.State, potionOwner);
        Assert.Equal(7, potionOwner.PlayerCombatState.Energy);
        Assert.Equal(1, targetPlayer.PlayerCombatState.Energy);
        Assert.Equal(3, radiance.Amount);

        for (int expectedAmount = 2; expectedAmount >= 0; expectedAmount--)
        {
            targetPlayer.PlayerCombatState.Energy = 0;
            await Hook.AfterEnergyReset(room.Engine.State, targetPlayer);

            Assert.Equal(1, targetPlayer.PlayerCombatState.Energy);
            Assert.Equal(expectedAmount, radiance.Amount);
            Assert.Equal(expectedAmount > 0, targetPlayer.Creature.Powers.Contains(radiance));
        }

        targetPlayer.PlayerCombatState.Energy = 0;
        await Hook.AfterEnergyReset(room.Engine.State, targetPlayer);
        Assert.Equal(0, targetPlayer.PlayerCombatState.Energy);
    }

    private static PotionModel GetCanonicalPotion(string potionName)
    {
        Type potionType = RequireTask8Type(potionName);
        return ModelDb.GetById<PotionModel>(ModelDb.GetId(potionType));
    }

    private static async Task UsePotionAsync(
        string potionName,
        Player owner,
        Creature? target)
    {
        PotionModel potion = owner.AddPotionInternal(GetCanonicalPotion(potionName));
        await PotionCmd.Use(potion, owner, target);
    }

    private static Type RequireTask8Type(string name)
    {
        string category = name.EndsWith("Power", StringComparison.Ordinal)
            ? "Powers"
            : "Potions";
        return typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.{category}.{name}")
            ?? throw new Xunit.Sdk.XunitException($"Task 8 model {name} is not implemented.");
    }

    private static IEnumerable<SovereignBlade> AllSovereignBlades(Player player) =>
        player.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .OfType<SovereignBlade>();

    private static Creature AddEnemy(CombatRoom room) =>
        room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            CombatSide.Enemy);

    private static async Task<(IReadOnlyList<Player> players, CombatRoom room)> CreateCombatAsync(
        string seed,
        int playerCount = 1)
    {
        var runState = new RunState(seed, new Overgrowth());
        var players = new List<Player>();
        for (int index = 0; index < playerCount; index++)
        {
            Player player = Player.CreateForNewRun(
                ModelDb.Character<UncommonPotionBatch2TestCharacter>(),
                runState);
            runState.AddPlayer(player);
            players.Add(player);
        }

        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (players, room);
    }
}

file sealed class UncommonPotionBatch2TestCharacter : CharacterModel
{
    public override int StartingHp => 75;

    public override int StartingGold => 99;
}
