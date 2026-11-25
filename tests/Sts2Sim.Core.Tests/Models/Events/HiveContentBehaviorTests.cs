using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class HiveContentBehaviorTests : IDisposable
{
    public HiveContentBehaviorTests() { ModelDb.ResetForTests(); ModelDb.Init(ContentRegistry.AllTypes); }
    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 5)]
    public async Task Metamorphosis_GeneratesFreeAttacksIntoDraw(bool upgraded, int count)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"metamorphosis-{upgraded}");
        Metamorphosis card = AddToHand<Metamorphosis>(player, upgraded);
        int rng = player.RunState.Rng.CombatCardGeneration.Counter;
        await room.Engine.PlayCardAsync(player, card, null);
        Assert.Equal(count, player.PlayerCombatState!.DrawPile.Cards.Count);
        Assert.All(player.PlayerCombatState.DrawPile.Cards, generated =>
        {
            Assert.Equal(CardType.Attack, generated.Type);
            Assert.Equal(0, generated.EnergyCost);
            Assert.Null(generated.TemporaryCostOverrideThisCombat);
            Assert.True(generated.TemporaryFreeThisCombat);
        });
        Assert.Equal(count, player.RunState.Rng.CombatCardGeneration.Counter - rng);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    public async Task Enlightenment_ReducesOnlyExpensiveCardsWithCorrectDuration(
        bool upgraded, bool thisTurn, bool thisCombat)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"enlightenment-{upgraded}");
        Enlightenment enlightenment = AddToHand<Enlightenment>(player, upgraded);
        Sneaky expensive = AddToHand<Sneaky>(player);
        Slice cheap = AddToHand<Slice>(player);
        Sneaky freed = AddToHand<Sneaky>(player);
        AscendersBane bane = AddToHand<AscendersBane>(player);
        freed.SetToFreeThisTurn();
        Assert.DoesNotContain(CardKeyword.Exhaust, enlightenment.Keywords);
        await room.Engine.PlayCardAsync(player, enlightenment, null);
        Assert.Equal(PileType.Discard, enlightenment.Pile!.Type);
        Assert.Equal(1, expensive.EnergyCost);
        Assert.Null(expensive.TemporaryCostOverrideThisTurn);
        Assert.Equal(thisTurn ? 1 : null, expensive.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Equal(thisCombat ? 1 : null, expensive.TemporaryCostOverrideThisCombat);
        Assert.Null(cheap.TemporaryCostOverrideThisTurn);
        // #1119：原版对整手牌 SetThis*(1, reduceOnly: true)，不筛费用；reduceOnly 使 0 费牌仍是 0，不被抬到 1。
        Assert.Equal(0, cheap.EnergyCost);
        Assert.Equal(thisTurn ? 1 : null, cheap.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Equal(thisCombat ? 1 : null, cheap.TemporaryCostOverrideThisCombat);
        // 已被压到 0 的牌：reduceOnly 不抬高；更低的修饰过期后它才生效（升级版本整场战斗有效）。
        Assert.Equal(0, freed.EnergyCost);
        freed.ClearTemporaryCostOverrideThisTurnOrUntilPlayed();
        Assert.Equal(upgraded ? 1 : freed.CanonicalEnergyCostValue, freed.EnergyCost);
        // 不可打出牌：设 1 费照常记录修饰（守卫只拦 0 费），有效费用仍是 -1。
        Assert.Equal(-1, bane.EnergyCost);
        Assert.Equal(thisTurn ? 1 : null, bane.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Equal(thisCombat ? 1 : null, bane.TemporaryCostOverrideThisCombat);
    }

    [Fact]
    public async Task PerfectFit_MovesItsCardToTopOnlyOnRealReshuffle()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("perfect-fit-shuffle");
        StrikeRegent fit = NewCard<StrikeRegent>(player);
        await CardCmd.Enchant<PerfectFit>(fit, 1m);
        CardPileCmd.Add(fit, PileType.Discard);
        CardPileCmd.Add(NewCard<DefendRegent>(player), PileType.Discard);
        await CardPileCmd.Shuffle(room.Engine.State, player);
        Assert.Same(fit, player.PlayerCombatState!.DrawPile.Cards[0]);
    }

    [Fact]
    public async Task BugslayerCards_ResolveDamageAndDebuffInCombat()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("bugslayer-cards", 2);
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        int[] hp = enemies.Select(e => e.CurrentHp).ToArray();
        await room.Engine.PlayCardAsync(player, AddToHand<Exterminate>(player), null);
        Assert.Equal(hp.Select(value => value - 12), enemies.Select(e => e.CurrentHp));
        Creature target = enemies[0];
        int before = target.CurrentHp;
        await room.Engine.PlayCardAsync(player, AddToHand<Squash>(player), target);
        Assert.Equal(before - 10, target.CurrentHp);
        Assert.Contains(target.Powers, power => power.GetType().Name == "VulnerablePower" && power.Amount == 2m);
    }

    [Fact]
    public async Task LostWisp_RelicDealsEightToEveryEnemyAfterOwnerPower()
    {
        (RunState run, Player player) = CreateRun("lost-wisp-relic");
        await RelicCmd.Obtain(ModelDb.Relic<Sts2Sim.Core.Models.Relics.LostWisp>(), player);
        CombatRoom room = await EnterCombat(run, 2);
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        int[] hp = enemies.Select(e => e.CurrentHp).ToArray();
        await room.Engine.PlayCardAsync(player, AddToHand<Calamity>(player), null);
        Assert.Equal(hp.Select(value => value - 8), enemies.Select(e => e.CurrentHp));
    }

    [Fact]
    public async Task SandCastle_UsesNonCombatGenerationAndUpgradesExactlySix()
    {
        (RunState run, Player player) = CreateRun("sand-castle");
        int eligible = player.Deck.Cards.Count(card => card.IsUpgradable);
        int rng = player.PlayerRng.NonCombatGeneration.Counter;
        CardModel[] expected = player.Deck.Cards.Where(card => card.IsUpgradable).ToList()
            .StableShuffle(player.PlayerRng.NonCombatGeneration.CloneExact()).Take(6).ToArray();
        await RelicCmd.Obtain(ModelDb.Relic<SandCastle>(), player);
        Assert.Equal(6, player.Deck.Cards.Count(card => card.IsUpgraded));
        // v0.113.1 StableShuffle seeds a child RNG: exactly one parent draw (#946).
        Assert.Equal(1, player.PlayerRng.NonCombatGeneration.Counter - rng);
        Assert.True(expected.ToHashSet().SetEquals(player.Deck.Cards.Where(card => card.IsUpgraded)),
            "seed=sand-castle: upgrades differ from native stable-shuffle selection");
        Assert.True(ModelDb.Relic<SandCastle>().HasUponPickupEffect);
    }

    [Fact]
    public async Task EnergyAndDrawAncients_ApplyTheirExactAmounts()
    {
        (RunState run, Player player) = CreateRun("hive-energy-draw");
        await RelicCmd.Obtain(ModelDb.Relic<PrismaticGem>(), player);
        var gem = Assert.Single(player.Relics.OfType<PrismaticGem>());
        var originalPool = player.Character.CardPool;
        CardCreationOptions Options(params CardPoolModel[] pools) =>
            new(pools, CardCreationSource.Encounter, CardRarityOddsType.Uniform, card => card is Anger);
        var merged = Hook.ModifyCardRewardCreationOptions(run, player,
            Options(originalPool, ColorlessCardPool.Instance).WithFlags(CardCreationFlags.IsCardReward));
        Assert.Equal(player.UnlockState.CharacterCardPools.Union([originalPool, ColorlessCardPool.Instance]).Select(pool => pool.GetType()),
            merged.CardPools.Select(pool => pool.GetType()));
        var reward = new Sts2Sim.Core.Rewards.CardReward(player, Options(originalPool), 1);
        reward.Populate(run);
        Assert.IsType<Anger>(Assert.Single(reward.Options));
        Assert.Equal([typeof(ColorlessCardPool)], Hook.ModifyCardRewardCreationOptions(run, player,
            Options(ColorlessCardPool.Instance).WithFlags(CardCreationFlags.IsCardReward))
            .CardPools.Select(pool => pool.GetType()));
        Assert.Equal([originalPool], Hook.ModifyCardRewardCreationOptions(run, player,
            Options(originalPool).WithFlags(CardCreationFlags.IsCardReward | CardCreationFlags.NoCardPoolModifications)).CardPools);
        Assert.Equal([originalPool], Hook.ModifyCardRewardCreationOptions(run, player,
            Options(originalPool)).CardPools);
        Player other = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        Assert.Equal([originalPool], gem.ModifyCardRewardCreationOptions(other,
            Options(originalPool).WithFlags(CardCreationFlags.IsCardReward)).CardPools);
        await RelicCmd.Obtain(ModelDb.Relic<PaelsBlood>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<VeryHotCocoa>(), player);
        CombatRoom room = await EnterCombat(run);
        Assert.Equal(player.MaxEnergy + 1 + 4, player.PlayerCombatState!.Energy);
        Assert.Equal(6m, Hook.ModifyHandDraw(room.Engine.State, player, 5m));
    }


    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed, int enemies = 1)
    {
        (RunState run, Player player) = CreateRun(seed);
        CombatRoom room = await EnterCombat(run, enemies);
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
            foreach (CardModel card in pile.Cards.ToArray()) CardPileCmd.Remove(card);
        player.PlayerCombatState.Energy = 99;
        return (player, room);
    }

    private static async Task<CombatRoom> EnterCombat(RunState run, int enemies = 1)
    {
        var room = new CombatRoom(() => Enumerable.Range(0, enemies)
            .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone()).ToArray());
        await room.Enter(run);
        return room;
    }

    private static (RunState Run, Player Player) CreateRun(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }

    private static T AddToHand<T>(Player player, bool upgraded = false) where T : CardModel
    {
        T card = NewCard<T>(player);
        if (upgraded) card.Upgrade();
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static T NewCard<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        return card;
    }
}
