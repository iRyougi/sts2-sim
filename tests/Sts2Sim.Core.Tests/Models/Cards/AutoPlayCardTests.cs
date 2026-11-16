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
            typeof(WanderingGrunt), typeof(TestSubject), typeof(AdaptablePower), typeof(EnragePower), typeof(StrengthPower),
            typeof(IAmInvincible), typeof(MakeItSo), typeof(Catastrophe), typeof(BeatDown),
            typeof(DecisionsDecisions), typeof(Bombardment), typeof(Clash), typeof(Neutralize), typeof(Dash),
            typeof(Ricochet), typeof(DaggerSpray),
            typeof(WeakPower), typeof(TheBomb), typeof(TheBombPower), typeof(SerpentFormPower), typeof(BurstPower),
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

        player.RunState.Rng.MockRng(RunRngType.CombatCardSelection, 8uL);
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
        int shuffleBefore = player.RunState.Rng.CombatCardSelection.Counter;

        await card.PlayAsync(target: null);

        Assert.Equal(hpBefore - 12, enemy.CurrentHp);
        Assert.Equal(blockBefore + 5, player.Creature.Block);
        Assert.Equal(targetsBefore + 4, player.RunState.Rng.CombatTargets.Counter);
        // v0.113.1（#946）：两次 StableShuffle 各让父流前进 1 次（含单张候选那次；旧版 1+0）。
        Assert.Equal(shuffleBefore + 2, player.RunState.Rng.CombatCardSelection.Counter);
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Catastrophe_LastEnemyKilled_LeavesLaterPickInDrawPile(bool upgraded, bool unplayable)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("catastrophe-last-enemy");
        foreach (CardModel c in player.PlayerCombatState!.DrawPile.Cards.ToList())
            CardPileCmd.Add(c, PileType.Discard);

        // v0.111 旧洗牌下 CombatCardSelection 种子 8 先选 Ricochet；v0.113.1（#946）子 Rng 洗牌下
        // 同一种子在 {Ricochet, TheBomb} 中先选 TheBomb（不换种子，按新版重算）。
        player.RunState.Rng.MockRng(RunRngType.CombatCardSelection, 8uL);
        AddTo<Ricochet>(player, PileType.Draw);
        TheBomb bomb = AddTo<TheBomb>(player, PileType.Draw);
        if (unplayable) bomb.AddKeywordInternal(CardKeyword.Unplayable);
        StrikeRegent fallback = AddTo<StrikeRegent>(player, PileType.Draw);
        fallback.AddKeywordInternal(CardKeyword.Unplayable);
        Catastrophe card = AddToHand<Catastrophe>(player);
        if (upgraded) card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp - 1, default);
        int playsBefore = player.PlayerCombatState.CardsPlayedThisTurn;
        int energyBefore = player.PlayerCombatState.Energy;
        int shuffleBefore = player.RunState.Rng.CombatCardSelection.Counter;

        await card.PlayAsync(target: null);

        Assert.True(room.Engine.IsEnding);
        Assert.True(enemy.IsDead);
        // unplayable=false：TheBomb 先被自动打出，Ricochet 再击杀，抽牌堆只剩 fallback；
        // unplayable=true：只有 Ricochet 可选，击杀后 TheBomb 与 fallback 留在抽牌堆。
        Assert.Equal(unplayable ? 2 : 1, player.PlayerCombatState.DrawPile.Cards.Count);
        if (unplayable)
        {
            Assert.Contains(bomb, player.PlayerCombatState.DrawPile.Cards);
            Assert.Null(player.Creature.GetPower<TheBombPower>());
        }
        else
        {
            Assert.DoesNotContain(bomb, player.PlayerCombatState.DrawPile.Cards);
            Assert.NotNull(player.Creature.GetPower<TheBombPower>());
        }
        Assert.Contains(fallback, player.PlayerCombatState.DrawPile.Cards);
        Assert.Equal(playsBefore + (unplayable ? 2 : 3), player.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Equal(energyBefore - 2, player.PlayerCombatState.Energy);
        // Native Catastrophe still selects on later iterations before AutoPlay refuses.
        // v0.113.1（#946）：每次 StableShuffle（含空列表、单张列表）父流都前进 1 次。
        // 逐轮：可出候选洗牌 1 次；可出候选为空时再加 fallback 整堆洗牌 1 次。
        int expectedShuffles = (upgraded, unplayable) switch
        {
            (false, false) => 2, // {Ricochet,Bomb}、{Ricochet}
            (true, false) => 4,  // 再加第 3 轮：{} + fallback {StrikeRegent}
            (false, true) => 3,  // {Ricochet}；第 2 轮 {} + fallback {Bomb,StrikeRegent}
            (true, true) => 5,   // 第 3 轮再 {} + fallback
        };
        Assert.Equal(shuffleBefore + expectedShuffles,
            player.RunState.Rng.CombatCardSelection.Counter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BeatDown_AutoPlaysThreeAttacksFromDiscard(bool includeUnplayableAttack)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beat-down");
        player.PlayerCombatState!.Energy = 10;
        player.RunState.Rng.MockRng(RunRngType.CombatCardSelection, 8uL);
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
        int shuffleBefore = player.RunState.Rng.CombatCardSelection.Counter;

        await card.PlayAsync(target: null);

        // v0.113.1（#946）：mock 种子 8 在子 Rng 洗牌下取前三张为 StrikeRegent、Clash、Neutralize（Dash 落在第四位，
        // 不换种子，按新版重算）：StrikeRegent 6 + Clash 14 + Neutralize 3。
        Assert.Equal(hpBefore - 23, enemy.CurrentHp);
        Assert.Equal(blockBefore, player.Creature.Block); // Dash 未被选中，不获得格挡。
        // 整表洗牌父流只前进 1 次（旧版 n-1=3 次）。
        Assert.Equal(shuffleBefore + 1, player.RunState.Rng.CombatCardSelection.Counter);
        Assert.Equal(7, player.PlayerCombatState.Energy);
        if (unplayable is not null)
            Assert.Contains(unplayable, player.PlayerCombatState.DiscardPile.Cards);

        // Native BeatDown passes null for RandomEnemy: Ricochet draws only its four hit targets.
        (Player randomPlayer, CombatRoom randomRoom) = await CreateCombatAsync("beat-down-ricochet-rng");
        AddTo<Ricochet>(randomPlayer, PileType.Discard);
        Creature randomEnemy = randomRoom.Engine.State.HittableEnemies.Single();
        int randomHpBefore = randomEnemy.CurrentHp;
        int targetsBefore = randomPlayer.RunState.Rng.CombatTargets.Counter;
        await AddToHand<BeatDown>(randomPlayer).PlayAsync(target: null);
        Assert.Equal(randomHpBefore - 12, randomEnemy.CurrentHp);
        Assert.Equal(targetsBefore + 4, randomPlayer.RunState.Rng.CombatTargets.Counter);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BeatDown_StopsWhenNoHittableEnemyRemains(bool upgraded)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("beat-down-last-enemy");
        for (int i = 0; i < 4; i++)
        {
            AddTo<DaggerSpray>(player, PileType.Discard);
        }

        Creature enemy = room.Engine.State.HittableEnemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp - 1, default);
        BeatDown card = AddToHand<BeatDown>(player);
        if (upgraded) card.Upgrade();
        int playsBefore = player.PlayerCombatState!.CardsPlayedThisTurn;
        int targetsBefore = player.RunState.Rng.CombatTargets.Counter;

        await card.PlayAsync(target: null);

        Assert.True(enemy.IsDead);
        Assert.Empty(room.Engine.State.HittableEnemies);
        Assert.Equal(playsBefore + 2, player.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Equal(targetsBefore, player.RunState.Rng.CombatTargets.Counter);

        // A dead TestSubject is still awaiting revival: empty targets do not imply combat ending.
        var revivalRun = new RunState("beat-down-reviving-target", new Overgrowth());
        Player revivalPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), revivalRun);
        revivalRun.AddPlayer(revivalPlayer);
        var revivalRoom = new CombatRoom(() => (TestSubject)ModelDb.Monster<TestSubject>().MutableClone());
        await revivalRoom.Enter(revivalRun);
        Creature subject = revivalRoom.Engine.State.HittableEnemies.Single();
        subject.LoseHpInternal(subject.CurrentHp - 1, default);
        await AddToHand<StrikeRegent>(revivalPlayer).PlayAsync(subject);
        Assert.True(subject.GetPower<AdaptablePower>()!.IsReviving);
        Assert.False(revivalRoom.Engine.IsOverOrEnding);
        Assert.Empty(revivalRoom.Engine.State.HittableEnemies);
        foreach (CardModel c in revivalPlayer.PlayerCombatState!.DiscardPile.Cards.ToList())
            CardPileCmd.Remove(c);
        StrikeRegent first = AddTo<StrikeRegent>(revivalPlayer, PileType.Discard);
        StrikeRegent second = AddTo<StrikeRegent>(revivalPlayer, PileType.Discard);
        first.ExhaustOnNextPlay = second.ExhaustOnNextPlay = true;
        BeatDown revivalCard = AddToHand<BeatDown>(revivalPlayer);
        if (upgraded) revivalCard.Upgrade();
        int revivalPlaysBefore = revivalPlayer.PlayerCombatState.CardsPlayedThisTurn;

        await revivalCard.PlayAsync(target: null);

        Assert.Equal(revivalPlaysBefore + 1, revivalPlayer.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Contains(first, revivalPlayer.PlayerCombatState.ExhaustPile.Cards);
        Assert.Contains(second, revivalPlayer.PlayerCombatState.ExhaustPile.Cards);
    }

    [Fact]
    public async Task DecisionsDecisions_PlaysFirstSkillCardThreeTimes()
    {
        (Player player, _) = await CreateCombatAsync("decisions-decisions");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        foreach (CardModel existing in player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToList())
            CardPileCmd.Remove(existing);
        DefendRegent[] draw = Enumerable.Range(0, 3)
            .Select(_ => AddTo<DefendRegent>(player, PileType.Draw)).ToArray();
        player.PlayerCombatState.GainStars(6);
        DecisionsDecisions card = AddToHand<DecisionsDecisions>(player);
        int blockBefore = player.Creature.Block;
        int startedBefore = player.PlayerCombatState.CardPlaysStartedThisTurn;
        int finishedBefore = player.PlayerCombatState.CardsPlayedThisTurn;

        await card.PlayAsync(target: null);

        Assert.Equal(blockBefore + 15, player.Creature.Block);
        Assert.Equal(startedBefore + 4, player.PlayerCombatState.CardPlaysStartedThisTurn);
        Assert.Equal(finishedBefore + 4, player.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Equal(draw.Skip(1), player.PlayerCombatState.Hand.Cards);
        Assert.Same(draw[0], Assert.Single(player.PlayerCombatState.DiscardPile.Cards));
        Assert.Same(card, Assert.Single(player.PlayerCombatState.ExhaustPile.Cards));

        // The first selected skill's AfterCardPlayed kills the last enemy; later repeats must stop.
        (Player endingPlayer, CombatRoom endingRoom) = await CreateCombatAsync("decisions-ending-bomb");
        foreach (CardModel c in endingPlayer.PlayerCombatState!.AllPiles.SelectMany(p => p.Cards).ToList())
            CardPileCmd.Remove(c);
        TheBomb bomb = AddToHand<TheBomb>(endingPlayer);
        DecisionsDecisions endingCard = AddToHand<DecisionsDecisions>(endingPlayer);
        endingRoom.Engine.State.CardSelectionSource = new LegacySelectionDecisionSource();
        Creature enemy = endingRoom.Engine.State.HittableEnemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp - 1, default);
        await PowerCmd.Apply<SerpentFormPower>(endingRoom.Engine.State, endingPlayer.Creature,
            1m, endingPlayer.Creature, null);

        await endingCard.PlayAsync(target: null);

        Assert.True(enemy.IsDead);
        Assert.Equal(2, endingPlayer.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Equal(PileType.Discard, bomb.Pile!.Type);
        Assert.Single(endingPlayer.Creature.Powers.OfType<TheBombPower>());

        // #686: a manual Burst replay must stop the same way (it used to dereference a null TheBombPower).
        (Player burstPlayer, CombatRoom burstRoom) = await CreateCombatAsync("burst-ending-bomb");
        foreach (CardModel c in burstPlayer.PlayerCombatState!.AllPiles.SelectMany(p => p.Cards).ToList())
            CardPileCmd.Remove(c);
        TheBomb burstBomb = AddToHand<TheBomb>(burstPlayer);
        Creature burstEnemy = burstRoom.Engine.State.HittableEnemies.Single();
        burstEnemy.LoseHpInternal(burstEnemy.CurrentHp - 1, default);
        await PowerCmd.Apply<SerpentFormPower>(burstRoom.Engine.State, burstPlayer.Creature,
            1m, burstPlayer.Creature, null);
        await PowerCmd.Apply<BurstPower>(burstRoom.Engine.State, burstPlayer.Creature,
            1m, burstPlayer.Creature, null);

        await burstBomb.PlayAsync(target: null);

        Assert.True(burstEnemy.IsDead);
        Assert.Equal(1, burstPlayer.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Single(burstPlayer.Creature.Powers.OfType<TheBombPower>());
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
