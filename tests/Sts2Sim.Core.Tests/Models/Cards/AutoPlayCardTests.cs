using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class AutoPlayCardTests : IDisposable
{
    public AutoPlayCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(IAmInvincible), typeof(MakeItSo), typeof(Catastrophe), typeof(BeatDown),
            typeof(DecisionsDecisions), typeof(Bombardment), typeof(Clash), typeof(Neutralize), typeof(Dash),
            typeof(Ricochet),
            typeof(WeakPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task IAmInvincible_OnDrawPileTop_AutoPlaysAfterAnotherCardIsPlayed()
    {
        (Player player, _) = await CreateCombatAsync("i-am-invincible");
        var card = (IAmInvincible)ModelDb.Card<IAmInvincible>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Top);
        DefendRegent other = AddToHand<DefendRegent>(player);
        int blockBefore = player.Creature.Block;

        await other.PlayAsync(target: null);

        // DefendRegent 的5点格挡 + IAmInvincible 自动出牌的10点格挡。
        Assert.Equal(blockBefore + 5 + 10, player.Creature.Block);
        Assert.NotEqual(PileType.Draw, card.Pile!.Type);
    }

    [Fact]
    public async Task MakeItSo_ThirdSkillPlayedThisTurn_ReturnsToHand()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("make-it-so");
        var card = (MakeItSo)ModelDb.Card<MakeItSo>().MutableClone();
        card.AssignOwner(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await card.PlayAsync(enemy);
        Assert.NotEqual(PileType.Hand, card.Pile!.Type);

        DefendRegent skill1 = AddToHand<DefendRegent>(player);
        await skill1.PlayAsync(target: null);
        DefendRegent skill2 = AddToHand<DefendRegent>(player);
        await skill2.PlayAsync(target: null);
        Assert.NotEqual(PileType.Hand, card.Pile!.Type);

        DefendRegent skill3 = AddToHand<DefendRegent>(player);
        await skill3.PlayAsync(target: null);

        Assert.Equal(PileType.Hand, card.Pile!.Type);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catastrophe_AutoPlaysTwoCardsFromDrawPile(bool includeUnplayableCard)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("catastrophe");
        foreach (CardModel c in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(c, PileType.Discard);
        }
        foreach (CardModel c in player.PlayerCombatState.DrawPile.Cards.ToList())
        {
            CardPileCmd.Add(c, PileType.Discard);
        }

        player.RunState.Rng.MockRng(RunRngType.Shuffle, 8uL);
        AddTo<Ricochet>(player, PileType.Draw);
        AddTo<DefendRegent>(player, PileType.Draw);
        StrikeRegent? unplayable = null;
        if (includeUnplayableCard)
        {
            unplayable = AddTo<StrikeRegent>(player, PileType.Draw);
            unplayable.AddKeywordInternal(CardKeyword.Unplayable);
        }

        Catastrophe card = AddToHand<Catastrophe>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        int blockBefore = player.Creature.Block;
        int targetsBefore = player.RunState.Rng.CombatTargets.Counter;
        int shuffleBefore = player.RunState.Rng.Shuffle.Counter;

        await card.PlayAsync(target: null);

        Assert.Equal(hpBefore - 12, enemy.CurrentHp);
        Assert.Equal(blockBefore + 5, player.Creature.Block);
        Assert.Equal(targetsBefore + 4, player.RunState.Rng.CombatTargets.Counter);
        Assert.Equal(shuffleBefore + 1, player.RunState.Rng.Shuffle.Counter);
        Assert.Equal(includeUnplayableCard ? 1 : 0, player.PlayerCombatState.DrawPile.Cards.Count);
        if (unplayable is not null)
        {
            Assert.Contains(unplayable, player.PlayerCombatState.DrawPile.Cards);

            // With no playable candidates left, the native fallback selects this card,
            // then CardCmd.AutoPlay moves it to the result pile without executing it.
            int playsBeforeFallback = player.PlayerCombatState.CardsPlayedThisTurn;
            await AddToHand<Catastrophe>(player).PlayAsync(target: null);
            Assert.Contains(unplayable, player.PlayerCombatState.DiscardPile.Cards);
            Assert.Equal(playsBeforeFallback + 1, player.PlayerCombatState.CardsPlayedThisTurn);
            Assert.Equal(hpBefore - 12, enemy.CurrentHp);
            Assert.Equal(targetsBefore + 4, player.RunState.Rng.CombatTargets.Counter);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BeatDown_AutoPlaysThreeAttacksFromDiscard(bool includeUnplayableAttack)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beat-down");
        player.PlayerCombatState!.Energy = 10;
        player.RunState.Rng.MockRng(RunRngType.Shuffle, 8uL);
        AddTo<StrikeRegent>(player, PileType.Discard);
        AddTo<Clash>(player, PileType.Discard);
        AddTo<Neutralize>(player, PileType.Discard);
        AddTo<Dash>(player, PileType.Discard);
        StrikeRegent? unplayable = null;
        if (includeUnplayableAttack)
        {
            unplayable = AddTo<StrikeRegent>(player, PileType.Discard);
            unplayable.AddKeywordInternal(CardKeyword.Unplayable);
        }

        BeatDown card = AddToHand<BeatDown>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        int blockBefore = player.Creature.Block;
        int shuffleBefore = player.RunState.Rng.Shuffle.Counter;

        await card.PlayAsync(target: null);

        Assert.Equal(hpBefore - 27, enemy.CurrentHp); // 原生：Clash 14 + Neutralize 3 + Dash 10。
        Assert.Equal(blockBefore + 10, player.Creature.Block); // Dash 必须被自动打出。
        Assert.Equal(shuffleBefore + 3, player.RunState.Rng.Shuffle.Counter);
        Assert.Equal(7, player.PlayerCombatState.Energy);
        if (unplayable is not null)
            Assert.Contains(unplayable, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task BeatDown_StopsWhenNoHittableEnemyRemains()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beat-down-last-enemy");
        for (int i = 0; i < 3; i++)
        {
            AddTo<StrikeRegent>(player, PileType.Discard);
        }

        Creature enemy = room.Engine.State.HittableEnemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp - 1, default);
        BeatDown card = AddToHand<BeatDown>(player);

        await card.PlayAsync(target: null);

        Assert.True(enemy.IsDead);
        Assert.Empty(room.Engine.State.HittableEnemies);
    }

    [Fact]
    public async Task DecisionsDecisions_PlaysFirstSkillCardThreeTimes()
    {
        (Player player, _) = await CreateCombatAsync("decisions-decisions");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        DecisionsDecisions card = AddToHand<DecisionsDecisions>(player);
        int blockBefore = player.Creature.Block;

        await card.PlayAsync(target: null);

        int defendPlays = (player.Creature.Block - blockBefore) / 5;
        Assert.True(defendPlays == 0 || defendPlays == 3);
    }

    [Fact]
    public async Task Bombardment_DealsDamageTwice()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("bombardment");
        Bombardment card = AddToHand<Bombardment>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 36, enemy.CurrentHp);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel =>
        AddTo<TCard>(player, PileType.Hand);

    private static TCard AddTo<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
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
