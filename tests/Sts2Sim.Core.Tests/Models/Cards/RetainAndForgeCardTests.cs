using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class RetainAndForgeCardTests : IDisposable
{
    public RetainAndForgeCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt), typeof(SovereignBlade),
            typeof(Equilibrium), typeof(Salvo), typeof(Purity), typeof(Restlessness),
            typeof(SummonForth), typeof(PanicButton),
            typeof(RetainHandPower), typeof(NoBlockPower), typeof(SpeedsterPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Equilibrium_GainsBlock_AndRetainsWholeHandAtEndOfTurn()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("equilibrium");
        CardModel other = AddToHand<StrikeRegent>(player);
        Equilibrium card = AddToHand<Equilibrium>(player);

        await card.PlayAsync(target: null);
        Assert.Equal(13, player.Creature.Block);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(other, player.PlayerCombatState!.Hand.Cards);
    }

    [Fact]
    public async Task Salvo_DealsDamage_AndRetainsWholeHandAtEndOfTurn()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("salvo");
        CardModel other = AddToHand<DefendRegent>(player);
        Salvo card = AddToHand<Salvo>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);
        Assert.Equal(hpBefore - 12, enemy.CurrentHp);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Contains(other, player.PlayerCombatState!.Hand.Cards);
    }

    [Fact]
    public async Task Purity_UpgradeExhaustsFiveCards_FromHand()
    {
        (Player player, _) = await CreateCombatAsync("purity");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        for (int i = 0; i < 6; i++)
        {
            AddToHand<StrikeRegent>(player);
        }

        Purity card = AddToHand<Purity>(player);
        card.Upgrade();
        int exhaustBefore = player.PlayerCombatState!.ExhaustPile.Cards.Count;

        await card.PlayAsync(target: null);

        // +5 张手牌被 Purity 主动耗尽,再 +1 是 Purity 自己（本身带 Exhaust 关键字,出牌后也会进耗尽堆）。
        Assert.Equal(exhaustBefore + 6, player.PlayerCombatState!.ExhaustPile.Cards.Count);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public async Task Restlessness_OnlyCardInHand_DrawsAndGainsEnergy(bool upgraded, int expectedDraw)
    {
        string seed = $"restlessness-alone-{upgraded}";
        (Player player, _) = await CreateCombatAsync(seed);
        foreach (CardModel c in player.PlayerCombatState!.Hand.Cards
                     .Concat(player.PlayerCombatState.DrawPile.Cards).ToList())
        {
            CardPileCmd.Add(c, PileType.Discard);
        }

        for (int i = 0; i < 3; i++)
        {
            var drawCard = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
            drawCard.AssignOwner(player);
            CardPileCmd.Add(drawCard, PileType.Draw);
        }

        Restlessness card = AddToHand<Restlessness>(player);
        if (upgraded) card.Upgrade();
        int energyBefore = player.PlayerCombatState!.Energy;

        await card.PlayAsync(target: null);

        Assert.True(player.PlayerCombatState!.Hand.Cards.Count == expectedDraw,
            $"seed={seed}, upgraded={upgraded}: should draw exactly {expectedDraw} cards");
        Assert.True(player.PlayerCombatState.DrawPile.Cards.Count == 3 - expectedDraw,
            $"seed={seed}, upgraded={upgraded}: unexpected draw-pile count");
        Assert.True(player.PlayerCombatState.Energy == energyBefore + expectedDraw,
            $"seed={seed}, upgraded={upgraded}: unexpected energy gain");

        string endingSeed = $"restlessness-ending-{upgraded}";
        (Player endingPlayer, CombatRoom endingRoom) = await CreateCombatAsync(endingSeed);
        foreach (CardModel c in endingPlayer.PlayerCombatState!.Hand.Cards
                     .Concat(endingPlayer.PlayerCombatState.DrawPile.Cards).ToList())
        {
            CardPileCmd.Add(c, PileType.Discard);
        }

        for (int i = 0; i < 3; i++)
        {
            var drawCard = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
            drawCard.AssignOwner(endingPlayer);
            CardPileCmd.Add(drawCard, PileType.Draw);
        }

        await PowerCmd.Apply<SpeedsterPower>(
            endingRoom.Engine.State, endingPlayer.Creature, 2m, endingPlayer.Creature, null);
        Creature enemy = endingRoom.Engine.State.Enemies.Single();
        enemy.SetCurrentHpInternal(1m);
        Restlessness endingCard = AddToHand<Restlessness>(endingPlayer);
        if (upgraded) endingCard.Upgrade();

        await endingCard.PlayAsync(target: null);

        Assert.True(enemy.CurrentHp == 0m,
            $"seed={endingSeed}, upgraded={upgraded}: first draw should defeat the only enemy");
        Assert.True(endingRoom.Engine.IsOverOrEnding,
            $"seed={endingSeed}, upgraded={upgraded}: combat should be ending");
        Assert.True(endingPlayer.PlayerCombatState.Hand.Cards.Count == 1,
            $"seed={endingSeed}, upgraded={upgraded}: draws should stop after combat ends");
        Assert.True(endingPlayer.PlayerCombatState.DrawPile.Cards.Count == 2,
            $"seed={endingSeed}, upgraded={upgraded}: only the first card should leave draw pile");
    }

    [Fact]
    public async Task Restlessness_NotOnlyCardInHand_NoEffect()
    {
        (Player player, _) = await CreateCombatAsync("restlessness-not-alone");
        AddToHand<StrikeRegent>(player);
        Restlessness card = AddToHand<Restlessness>(player);
        int energyBefore = player.PlayerCombatState!.Energy;

        await card.PlayAsync(target: null);

        Assert.Equal(energyBefore, player.PlayerCombatState!.Energy);
    }

    [Fact]
    public async Task SummonForth_RecallsSovereignBlades_AndForges()
    {
        (Player player, _) = await CreateCombatAsync("summon-forth");
        var blade = (SovereignBlade)ModelDb.Card<SovereignBlade>().MutableClone();
        blade.AssignOwner(player);
        CardPileCmd.Add(blade, PileType.Discard);

        SummonForth card = AddToHand<SummonForth>(player);
        await card.PlayAsync(target: null);

        Assert.Contains(blade, player.PlayerCombatState!.Hand.Cards);
    }

    [Fact]
    public async Task PanicButton_GainsBlock_AndBlocksFurtherBlockForTwoTurns()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("panic-button");
        PanicButton card = AddToHand<PanicButton>(player);

        await card.PlayAsync(target: null);
        Assert.Equal(30, player.Creature.Block);

        DefendRegent defend = AddToHand<DefendRegent>(player);
        await defend.PlayAsync(target: null);

        Assert.Equal(30, player.Creature.Block);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
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
