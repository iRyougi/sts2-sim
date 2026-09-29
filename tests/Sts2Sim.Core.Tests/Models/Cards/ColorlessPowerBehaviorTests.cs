using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class ColorlessPowerBehaviorTests : IDisposable
{
    public ColorlessPowerBehaviorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Fasten), typeof(Finesse), typeof(Panache), typeof(PrepTime), typeof(TheBomb),
            typeof(FastenPower), typeof(PanachePower), typeof(PrepTimePower), typeof(TheBombPower),
            typeof(VigorPower), typeof(WeakPower), typeof(VulnerablePower),
            typeof(DivineRight), typeof(WanderingGrunt), typeof(HardyBrute),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 11)]
    public async Task Fasten_OnlyAddsBlockToDefendTaggedCards(bool upgraded, int expectedDefendBlock)
    {
        (Player player, _) = await CreateCombatAsync($"fasten-{upgraded}");
        Fasten fasten = AddToHand<Fasten>(player);
        if (upgraded) fasten.Upgrade();
        await fasten.PlayAsync(target: null);

        DefendRegent defend = AddToHand<DefendRegent>(player);
        await defend.PlayAsync(target: null);
        Assert.Equal(expectedDefendBlock, player.Creature.Block);

        player.Creature.LoseBlockInternal(player.Creature.Block);
        Finesse finesse = AddToHand<Finesse>(player);
        await finesse.PlayAsync(target: null);
        Assert.Equal(4, player.Creature.Block);

        player.Creature.LoseBlockInternal(player.Creature.Block);
        await CreatureCmd.GainBlock(
            player.Creature.CombatState!, player.Creature, 2m, ValueProp.Move, null, null);
        Assert.Equal(upgraded ? 8 : 6, player.Creature.Block);

        player.Creature.LoseBlockInternal(player.Creature.Block);
        await CreatureCmd.GainBlock(
            player.Creature.CombatState!, player.Creature, 2m, ValueProp.Unpowered, null, null);
        Assert.Equal(2, player.Creature.Block);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public async Task PrepTime_GrantsVigorAtEachOwnerTurnStart(bool upgraded, int expectedVigor)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"prep-time-{upgraded}");
        PrepTime card = AddToHand<PrepTime>(player);
        if (upgraded) card.Upgrade();
        await card.PlayAsync(target: null);

        await Hook.AfterSideTurnStart(
            room.Engine.State, CombatSide.Player, room.Engine.State.Allies);

        Assert.Equal(expectedVigor, player.Creature.GetPower<VigorPower>()!.Amount);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 14)]
    public async Task Panache_DamagesAllEnemiesAfterFiveSubsequentCards(bool upgraded, int expectedDamage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"panache-{upgraded}");
        Panache panache = AddToHand<Panache>(player);
        if (upgraded) panache.Upgrade();
        await panache.PlayAsync(target: null);

        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;
        CardModel playedCard = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        playedCard.AssignOwner(player);
        CardPlay cardPlay = CreateCardPlay(playedCard, player);
        for (int i = 0; i < 4; i++)
        {
            await Hook.AfterCardPlayed(room.Engine.State, cardPlay);
        }
        Assert.Equal(hpBefore, room.Engine.State.Enemies.Single().CurrentHp);

        await Hook.AfterCardPlayed(room.Engine.State, cardPlay);
        Assert.Equal(hpBefore - expectedDamage, room.Engine.State.Enemies.Single().CurrentHp);
    }

    [Fact]
    public async Task Panache_ResetsProgressAtOwnerTurnEnd()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("panache-reset");
        Panache panache = AddToHand<Panache>(player);
        await panache.PlayAsync(target: null);

        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;
        CardModel playedCard = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        playedCard.AssignOwner(player);
        CardPlay cardPlay = CreateCardPlay(playedCard, player);
        for (int i = 0; i < 4; i++)
        {
            await Hook.AfterCardPlayed(room.Engine.State, cardPlay);
        }

        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        await Hook.AfterCardPlayed(room.Engine.State, cardPlay);

        Assert.Equal(hpBefore, room.Engine.State.Enemies.Single().CurrentHp);
    }

    [Fact]
    public async Task Panache_InstancesTrackCardsIndependently()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("panache-instances");
        await AddToHand<Panache>(player).PlayAsync(target: null);
        await AddToHand<Panache>(player).PlayAsync(target: null);
        Assert.Equal(2, player.Creature.Powers.OfType<PanachePower>().Count());

        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;
        CardModel playedCard = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        playedCard.AssignOwner(player);
        CardPlay cardPlay = CreateCardPlay(playedCard, player);
        for (int i = 0; i < 4; i++)
        {
            await Hook.AfterCardPlayed(room.Engine.State, cardPlay);
        }
        Assert.Equal(hpBefore - 10, room.Engine.State.Enemies.Single().CurrentHp);

        await Hook.AfterCardPlayed(room.Engine.State, cardPlay);
        Assert.Equal(hpBefore - 20, room.Engine.State.Enemies.Single().CurrentHp);
    }

    [Fact]
    public async Task TheBomb_InstancesKeepIndependentDamage()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("the-bomb-instances", hardy: true);
        await AddToHand<TheBomb>(player).PlayAsync(target: null);
        TheBomb upgraded = AddToHand<TheBomb>(player);
        upgraded.Upgrade();
        await upgraded.PlayAsync(target: null);

        Assert.Equal(2, player.Creature.Powers.OfType<TheBombPower>().Count());
        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;
        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.All(player.Creature.Powers.OfType<TheBombPower>(), power => Assert.Equal(1, power.Amount));

        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(Math.Max(0, hpBefore - 90), room.Engine.State.Enemies.Single().CurrentHp);
        Assert.Empty(player.Creature.Powers.OfType<TheBombPower>());
    }
    [Theory]
    [InlineData(false, 40)]
    [InlineData(true, 50)]
    public async Task TheBomb_ExplodesAfterThreeOwnerTurnEnds(bool upgraded, int expectedDamage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"the-bomb-{upgraded}");
        TheBomb bomb = AddToHand<TheBomb>(player);
        if (upgraded) bomb.Upgrade();
        await bomb.PlayAsync(target: null);

        TheBombPower power = player.Creature.GetPower<TheBombPower>()!;
        Assert.Equal(3, power.Amount);
        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;

        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(hpBefore, room.Engine.State.Enemies.Single().CurrentHp);

        await Hook.BeforeSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(Math.Max(0, hpBefore - expectedDamage), room.Engine.State.SpawnedEnemies.Single().CurrentHp);
        Assert.Null(player.Creature.GetPower<TheBombPower>());
    }

    private static CardPlay CreateCardPlay(CardModel card, Player player) => new()
    {
        Card = card,
        Player = player,
        Target = null,
        ResultPile = PileType.Discard,
        Resources = new ResourceInfo(0, 0, 0, 0),
        IsAutoPlay = false,
        PlayIndex = 0,
        PlayCount = 1,
    };

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        player.PlayerCombatState.Energy = 10;
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed, bool hardy = false)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => hardy
            ? (HardyBrute)ModelDb.Monster<HardyBrute>().MutableClone()
            : (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
