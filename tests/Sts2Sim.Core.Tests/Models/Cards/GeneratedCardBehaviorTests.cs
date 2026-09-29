using System.Reflection;
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
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedCardBehaviorTests : IDisposable
{
    public GeneratedCardBehaviorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AstralPulse_Upgrade_DealsEightDamageTwiceToAllEnemies()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("astral-pulse");
        AstralPulse card = AddToHand<AstralPulse>(player);
        card.Upgrade();
        player.PlayerCombatState!.GainStars(3);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(target: null);

        Assert.Equal(hpBefore - 16, enemy.CurrentHp);
    }

    [Fact]
    public async Task Patter_Upgrade_GainsBlockAndVigor()
    {
        (Player player, _) = await CreateCombatAsync("patter");
        Patter card = AddToHand<Patter>(player);
        card.Upgrade();

        await card.PlayAsync(target: null);

        Assert.Equal(10, player.Creature.Block);
        Assert.Equal(3, player.Creature.Powers.OfType<VigorPower>().Single().Amount);
    }

    [Fact]
    public void KnowThyPlace_Upgrade_RemovesExhaust()
    {
        var card = (KnowThyPlace)ModelDb.Card<KnowThyPlace>().MutableClone();
        Assert.True(card.HasKeyword(CardKeyword.Exhaust));

        card.Upgrade();

        Assert.False(card.HasKeyword(CardKeyword.Exhaust));
    }

    [Fact]
    public async Task Volley_SpendsAllEnergyAsHitCount()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("volley");
        Volley card = AddToHand<Volley>(player);
        player.PlayerCombatState!.Energy = 3;
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(target: null);

        Assert.Equal(hpBefore - 30, enemy.CurrentHp);
        Assert.Equal(0, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task BigBang_PlaysWithoutTarget_AndDoesNotDealDamage()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("big-bang");
        // Pre-seed a SovereignBlade so Forge does not generate an extra one into hand,
        // keeping the hand-size delta isolated to BigBang's own play/draw.
        AddToHand<SovereignBlade>(player);
        BigBang card = AddToHand<BigBang>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int enemyHpBefore = enemy.CurrentHp;
        int energyBefore = player.PlayerCombatState!.Energy;
        int starsBefore = player.PlayerCombatState.Stars;
        int handCountBefore = player.PlayerCombatState.Hand.Cards.Count;

        await card.PlayAsync(target: null);

        Assert.Equal(enemyHpBefore, enemy.CurrentHp);
        Assert.Equal(energyBefore + 1, player.PlayerCombatState.Energy);
        Assert.Equal(starsBefore + 1, player.PlayerCombatState.Stars);
        // BigBang exhausts itself (-1) then draws 1 card, so hand size is unchanged.
        Assert.Equal(handCountBefore, player.PlayerCombatState.Hand.Cards.Count);
    }

    [Fact]
    public async Task Alignment_Upgrade_GainsThreeEnergy()
    {
        (Player player, _) = await CreateCombatAsync("alignment-upgrade");
        Alignment card = AddToHand<Alignment>(player);
        card.Upgrade();
        player.PlayerCombatState!.Energy = 0;

        await card.PlayAsync(target: null);

        Assert.Equal(3, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task DyingStar_ReducesEveryEnemyStrengthByNineUntilEnemyTurnEnds()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("dying-star-base");
        room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            Sts2Sim.Core.Combat.CombatSide.Enemy);
        DyingStar card = AddToHand<DyingStar>(player);
        player.PlayerCombatState!.GainStars(3);
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        int[] hpBefore = enemies.Select(enemy => enemy.CurrentHp).ToArray();

        await card.PlayAsync(target: null);

        for (int i = 0; i < enemies.Length; i++)
        {
            Assert.Equal(hpBefore[i] - 9, enemies[i].CurrentHp);
            Assert.Equal(9m, enemies[i].GetPower<DyingStarPower>()!.Amount);
        }

        await Sts2Sim.Core.Hooks.Hook.AfterSideTurnEnd(
            room.Engine.State,
            Sts2Sim.Core.Combat.CombatSide.Enemy,
            room.Engine.State.Enemies);

        Assert.All(enemies, enemy => Assert.Null(enemy.GetPower<DyingStarPower>()));
    }

    [Fact]
    public async Task DyingStar_Upgrade_IncreasesDamageAndStrengthLossByTwo()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("dying-star-upgrade");
        DyingStar card = AddToHand<DyingStar>(player);
        card.Upgrade();
        player.PlayerCombatState!.GainStars(3);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(target: null);

        Assert.Equal(hpBefore - 11, enemy.CurrentHp);
        Assert.Equal(11m, enemy.GetPower<DyingStarPower>()!.Amount);
    }

    [Fact]
    public async Task DyingStar_RepeatedPlays_StackTemporaryStrengthLoss()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("dying-star-stack");
        DyingStar first = AddToHand<DyingStar>(player);
        DyingStar second = AddToHand<DyingStar>(player);
        player.PlayerCombatState!.GainStars(6);
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await first.PlayAsync(target: null);
        await second.PlayAsync(target: null);

        Assert.Equal(18m, enemy.GetPower<DyingStarPower>()!.Amount);

        await Sts2Sim.Core.Hooks.Hook.AfterSideTurnEnd(
            room.Engine.State,
            Sts2Sim.Core.Combat.CombatSide.Enemy,
            room.Engine.State.Enemies);

        Assert.Null(enemy.GetPower<DyingStarPower>());
    }

    [Fact]
    public async Task UpgradedParry_PlaysWithoutTarget_AndAppliesFourteenPower()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("parry-upgrade");
        Parry card = AddToHand<Parry>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int enemyHpBefore = enemy.CurrentHp;

        await card.PlayAsync(target: null);

        Assert.Equal(enemyHpBefore, enemy.CurrentHp);
        Assert.Equal(14m, player.Creature.Powers.OfType<ParryPower>().Single().Amount);
    }

    [Fact]
    public void ZeroDamageGeneratedCards_DoNotIntroduceDamageOnlyWhenUpgraded()
    {
        PropertyInfo specProperty = typeof(GeneratedCardModel).GetProperty(
            "Spec",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        string[] offenders = ModelDb.All<CardModel>()
            .OfType<GeneratedCardModel>()
            .Select(card => (Type: card.GetType(), Spec: (GeneratedCardSpec)specProperty.GetValue(card)!))
            .Where(pair => pair.Spec.Damage == 0m && pair.Spec.UpgradeDamage != 0m)
            .Select(pair => pair.Type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task UpgradedGeneratedCards_ApplyTransferredResourceAndPowerAmounts()
    {
        (Player energyPlayer, _) = await CreateCombatAsync("upgrade-energy");
        BelieveInYou energy = AddToHand<BelieveInYou>(energyPlayer);
        energy.Upgrade();
        energyPlayer.PlayerCombatState!.Energy = 0;
        await energy.PlayAsync(energyPlayer.Creature);
        Assert.Equal(3, energyPlayer.PlayerCombatState.Energy);

        (Player strengthPlayer, _) = await CreateCombatAsync("upgrade-strength");
        Coordinate strength = AddToHand<Coordinate>(strengthPlayer);
        strength.Upgrade();
        await strength.PlayAsync(strengthPlayer.Creature);
        Assert.Equal(8m, strengthPlayer.Creature.GetPower<CoordinatePower>()!.Amount);

        (Player debuffPlayer, CombatRoom debuffRoom) = await CreateCombatAsync("upgrade-debuff");
        Shockwave debuff = AddToHand<Shockwave>(debuffPlayer);
        debuff.Upgrade();
        debuffPlayer.PlayerCombatState!.Energy = 2;
        await debuff.PlayAsync(target: null);
        Creature enemy = debuffRoom.Engine.State.HittableEnemies.Single();
        Assert.Equal(5m, enemy.GetPower<WeakPower>()!.Amount);
        Assert.Equal(5m, enemy.GetPower<VulnerablePower>()!.Amount);

        (Player vigorPlayer, _) = await CreateCombatAsync("upgrade-vigor");
        Terraforming vigor = AddToHand<Terraforming>(vigorPlayer);
        vigor.Upgrade();
        await vigor.PlayAsync(target: null);
        Assert.Equal(10m, vigorPlayer.Creature.GetPower<VigorPower>()!.Amount);

        (Player productionPlayer, _) = await CreateCombatAsync("upgrade-production");
        Production production = AddToHand<Production>(productionPlayer);
        production.Upgrade();
        productionPlayer.PlayerCombatState!.Energy = 0;
        await production.PlayAsync(target: null);
        Assert.Equal(3, productionPlayer.PlayerCombatState.Energy);

        (Player resonancePlayer, _) = await CreateCombatAsync("upgrade-resonance");
        Resonance resonance = AddToHand<Resonance>(resonancePlayer);
        resonance.Upgrade();
        resonancePlayer.PlayerCombatState!.Energy = 1;
        await PlayerCmd.GainStars(2, resonancePlayer);
        await resonance.PlayAsync(target: null);
        Assert.Equal(2m, resonancePlayer.Creature.GetPower<StrengthPower>()!.Amount);
    }

    [Fact]
    public async Task UpgradedGeneratedCards_ApplyDrawForgeAndGeneratedPowerAmounts()
    {
        await AssertUpgradedDrawAsync<HuddleUp>("upgrade-huddle", expectedDraw: 3);
        await AssertUpgradedDrawAsync<MasterOfStrategy>("upgrade-master", expectedDraw: 4);
        await AssertUpgradedDrawAsync<Prophesize>("upgrade-prophesize", expectedDraw: 9);

        (Player forgePlayer, _) = await CreateCombatAsync("upgrade-forge");
        TheSmith forge = AddToHand<TheSmith>(forgePlayer);
        forge.Upgrade();
        forgePlayer.PlayerCombatState!.Energy = 1;
        await PlayerCmd.GainStars(4, forgePlayer);
        await forge.PlayAsync(target: null);
        SovereignBlade blade = forgePlayer.PlayerCombatState.Hand.Cards.OfType<SovereignBlade>().Single();
        FieldInfo damageField = typeof(SovereignBlade).GetField(
            "_damage",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(50m, damageField.GetValue(blade));

        (Player plotPlayer, _) = await CreateCombatAsync("upgrade-plot");
        Plot plot = AddToHand<Plot>(plotPlayer);
        plot.Upgrade();
        await plot.PlayAsync(target: null);
        Assert.Equal(3m, plotPlayer.Creature.GetPower<DrawCardsNextTurnPower>()!.Amount);

        (Player gambitPlayer, _) = await CreateCombatAsync("upgrade-gambit");
        TheGambit gambit = AddToHand<TheGambit>(gambitPlayer);
        gambit.Upgrade();
        await gambit.PlayAsync(target: null);
        Assert.Equal(75m, gambitPlayer.Creature.Block);
        Assert.Equal(1m, gambitPlayer.Creature.GetPower<TheGambitPower>()!.Amount);
    }

    // v0.111.0 正式版（41cef1ea）的数值，期望值直接写正式版，不从实现读取（#67）。Mirage 由 #70 处理。
    [Theory]
    [InlineData("Alignment", false)]
    [InlineData("Alignment", true)]
    [InlineData("BrightestFlame", false)]
    [InlineData("BrightestFlame", true)]
    [InlineData("GuidingStar", false)]
    [InlineData("GuidingStar", true)]
    [InlineData("RefineBlade", false)]
    [InlineData("RefineBlade", true)]
    [InlineData("Rend", false)]
    [InlineData("Rend", true)]
    [InlineData("SpoilsOfBattle", false)]
    [InlineData("SpoilsOfBattle", true)]
    public async Task V0111CardDrift_MatchesOfficialValues(string cardName, bool upgraded)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"v0111-drift-{cardName}-{upgraded}");
        PlayerCombatState state = player.PlayerCombatState!;
        var card = (CardModel)ModelDb.All<CardModel>().Single(c => c.GetType().Name == cardName).MutableClone();
        card.AssignOwner(player);
        state.Hand.AddInternal(card);
        if (upgraded)
        {
            card.Upgrade();
        }

        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int enemyHpBefore = enemy.CurrentHp;
        int handBefore = state.Hand.Cards.Count;
        int maxHpBefore = player.Creature.MaxHp;
        state.Energy = 0;
        switch (cardName)
        {
            case "Alignment":
                Assert.Equal(2, card.StarCost);
                await card.PlayAsync(target: null);
                Assert.Equal(upgraded ? 3 : 2, state.Energy);
                break;
            case "BrightestFlame":
                await card.PlayAsync(target: null);
                Assert.Equal(upgraded ? 3 : 2, state.Energy);
                Assert.Equal(handBefore - 1 + (upgraded ? 3 : 2), state.Hand.Cards.Count);
                Assert.Equal(maxHpBefore - 2, player.Creature.MaxHp);
                break;
            case "GuidingStar":
                Assert.Equal(1, card.StarCost);
                await card.PlayAsync(enemy);
                Assert.Equal(enemyHpBefore - (upgraded ? 13 : 12), enemy.CurrentHp);
                Assert.Equal(handBefore - 1, state.Hand.Cards.Count);
                Assert.Equal(upgraded ? 3 : 2, Assert.Single(player.Creature.Powers.OfType<DrawCardsNextTurnPower>()).Amount);
                break;
            case "Rend":
                Assert.Equal(1, card.EnergyCost);
                await PowerCmd.Apply<WeakPower>(room.Engine.State, enemy, 1m, player.Creature, null);
                await card.PlayAsync(enemy);
                Assert.Equal(enemyHpBefore - (upgraded ? 12 + 8 : 10 + 5), enemy.CurrentHp);
                break;
            case "RefineBlade":
            case "SpoilsOfBattle":
                await card.PlayAsync(target: null);
                SovereignBlade blade = Assert.Single(state.Hand.Cards.OfType<SovereignBlade>());
                state.Energy = 99;
                await blade.PlayAsync(enemy);
                int forge = cardName == "RefineBlade" ? (upgraded ? 12 : 8) : (upgraded ? 9 : 6);
                Assert.Equal(enemyHpBefore - (10 + forge), enemy.CurrentHp);
                break;
        }
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private static async Task AssertUpgradedDrawAsync<TCard>(string seed, int expectedDraw)
        where TCard : CardModel
    {
        (Player player, _) = await CreateCombatAsync(seed);
        foreach (CardModel handCard in player.PlayerCombatState!.Hand.Cards.ToArray())
        {
            CardPileCmd.Add(handCard, PileType.Discard);
        }
        TCard card = AddToHand<TCard>(player);
        card.Upgrade();
        player.PlayerCombatState.Energy = 99;

        await card.PlayAsync(target: null);

        Assert.Equal(expectedDraw, player.PlayerCombatState.Hand.Cards.Count);
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
