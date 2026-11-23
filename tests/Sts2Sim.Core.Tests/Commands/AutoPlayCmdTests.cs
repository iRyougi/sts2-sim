using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
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

namespace Sts2Sim.Core.Tests.Commands;

[Collection("ModelDb")]
public sealed class AutoPlayCmdTests : IDisposable
{
    public AutoPlayCmdTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt), typeof(Volley), typeof(AutoPlayResourceProbeCard),
            typeof(TheBomb), typeof(TheBombPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task FromTopOfDrawPile_PlaysExactlyTheTopTwoCardsInOrder()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("auto-play-top-order");
        EmptyHandIntoDiscardPile(player);
        var playOrder = new List<string>();
        SequencedSkillCard third = AddSequencedCard(player, playOrder, "third", CardPilePosition.Top);
        _ = AddSequencedCard(player, playOrder, "second", CardPilePosition.Top);
        _ = AddSequencedCard(player, playOrder, "top", CardPilePosition.Top);

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, count: 2);

        Assert.Equal(new[] { "top", "second" }, playOrder);
        Assert.Equal(PileType.Draw, third.Pile!.Type);

        // Native preloads the batch into Play, then refuses later skills after victory.
        (Player endingPlayer, CombatRoom endingRoom) = await CreateCombatAsync("auto-play-ending-bomb");
        ClearCombatPiles(endingPlayer);
        TheBomb bomb = AddTo<TheBomb>(endingPlayer, PileType.Draw, CardPilePosition.Bottom);
        bomb.ExhaustOnNextPlay = true;
        TheBomb unplayable = AddTo<TheBomb>(endingPlayer, PileType.Draw, CardPilePosition.Bottom);
        unplayable.AddKeywordInternal(CardKeyword.Unplayable);
        unplayable.ExhaustOnNextPlay = true;
        AddTo<StrikeRegent>(endingPlayer, PileType.Draw, CardPilePosition.Top);
        Creature enemy = endingRoom.Engine.State.HittableEnemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp - 1, default);
        await AutoPlayCmd.FromTopOfDrawPile(endingRoom.Engine.State, endingPlayer, count: 3);
        Assert.True(enemy.IsDead);
        Assert.Equal(1, endingPlayer.PlayerCombatState!.CardsPlayedThisTurn);
        Assert.Equal(PileType.Play, bomb.Pile!.Type);
        Assert.Equal(PileType.Play, unplayable.Pile!.Type);
        Assert.False(bomb.ExhaustOnNextPlay);
        Assert.False(unplayable.ExhaustOnNextPlay);
        Assert.Null(endingPlayer.Creature.GetPower<TheBombPower>());

        // Generic batches retain the original pile for cards refused after the first kill.
        (Player batchPlayer, CombatRoom batchRoom) = await CreateCombatAsync("auto-play-generic-ending");
        ClearCombatPiles(batchPlayer);
        StrikeRegent strike = AddTo<StrikeRegent>(batchPlayer, PileType.Draw);
        TheBomb skippedBomb = AddTo<TheBomb>(batchPlayer, PileType.Draw);
        skippedBomb.ExhaustOnNextPlay = true;
        Creature batchEnemy = batchRoom.Engine.State.HittableEnemies.Single();
        batchEnemy.LoseHpInternal(batchEnemy.CurrentHp - 1, default);
        await AutoPlayCmd.FromCards(batchRoom.Engine.State, batchPlayer, [strike, skippedBomb]);
        Assert.True(batchEnemy.IsDead);
        Assert.Equal(1, batchPlayer.PlayerCombatState!.CardsPlayedThisTurn);
        Assert.Equal(PileType.Draw, skippedBomb.Pile!.Type);
        Assert.True(skippedBomb.ExhaustOnNextPlay);
        Assert.Null(batchPlayer.Creature.GetPower<TheBombPower>());
    }

    [Fact]
    public async Task FromTopOfDrawPile_ShufflesDiscardAndContinuesUntilCount()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("auto-play-shuffle");
        ClearCombatPiles(player);
        var playOrder = new List<string>();
        _ = AddSequencedCard(player, playOrder, "draw", CardPilePosition.Top);
        SequencedSkillCard discard = (SequencedSkillCard)new SequencedSkillCard(playOrder, "discard").MutableClone();
        discard.AssignOwner(player);
        CardPileCmd.Add(discard, PileType.Discard);

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, count: 2);

        Assert.Equal(2, playOrder.Count);
        Assert.Equal("draw", playOrder[0]);
        Assert.Empty(player.PlayerCombatState!.DrawPile.Cards);
    }

    [Fact]
    public async Task FromTopOfDrawPile_AutoPlayDoesNotSpendResourcesAndMarksCardPlay()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("auto-play-resources");
        ClearCombatPiles(player);
        AutoPlayResourceProbeCard card = AddTo<AutoPlayResourceProbeCard>(
            player,
            PileType.Draw,
            CardPilePosition.Top);
        await PlayerCmd.GainStars(3, player);
        int energyBefore = player.PlayerCombatState!.Energy;
        int starsBefore = player.PlayerCombatState.Stars;

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, count: 1);

        Assert.Equal(energyBefore, player.PlayerCombatState.Energy);
        Assert.Equal(starsBefore, player.PlayerCombatState.Stars);
        Assert.NotNull(card.LastPlay);
        CardPlay play = card.LastPlay;
        Assert.True(play.IsAutoPlay);
        Assert.Equal(0, play.Resources.EnergySpent);
        Assert.Equal(2, play.Resources.EnergyValue);
        Assert.Equal(0, play.Resources.StarsSpent);
        Assert.Equal(1, play.Resources.StarValue);
    }

    [Fact]
    public async Task FromTopOfDrawPile_AutoPlayedXCardUsesCapturedValueWithoutSpendingEnergy()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("auto-play-x-value");
        ClearCombatPiles(player);
        AddTo<Volley>(player, PileType.Draw, CardPilePosition.Top);
        player.PlayerCombatState!.Energy = 3;
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, count: 1);

        Assert.Equal(hpBefore - 30, enemy.CurrentHp);
        Assert.Equal(3, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task FromTopOfDrawPile_UsesCombatTargetsPredictedEnemy()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("auto-play-combat-targets");
        EmptyHandIntoDiscardPile(player);
        AddTo<StrikeRegent>(player, PileType.Draw, CardPilePosition.Top);
        room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            CombatSide.Enemy);
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        Dictionary<Creature, int> hpBefore = enemies.ToDictionary(enemy => enemy, enemy => enemy.CurrentHp);
        var expectedRng = room.Engine.State.RunState.Rng.CombatTargets.CloneExact();
        Creature expectedTarget = expectedRng.NextItem(enemies)!;
        Assert.Same(enemies[1], expectedTarget);

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, count: 1);

        Assert.Equal(hpBefore[expectedTarget] - 6, expectedTarget.CurrentHp);
        Assert.All(
            enemies.Where(enemy => enemy != expectedTarget),
            enemy => Assert.Equal(hpBefore[enemy], enemy.CurrentHp));
        Assert.Equal(expectedRng.NextInt(1000), room.Engine.State.RunState.Rng.CombatTargets.NextInt(1000));
    }

    private static void EmptyHandIntoDrawPile(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Draw, CardPilePosition.Bottom);
        }
    }

    private static void EmptyHandIntoDiscardPile(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.Concat(player.PlayerCombatState.DrawPile.Cards).ToList())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }

    private static void ClearCombatPiles(Player player)
    {
        PlayerCombatState state = player.PlayerCombatState!;
        foreach (CardModel card in state.Hand.Cards
                     .Concat(state.DrawPile.Cards)
                     .Concat(state.DiscardPile.Cards)
                     .Concat(state.ExhaustPile.Cards)
                     .Concat(state.PlayPile.Cards)
                     .ToList())
        {
            CardPileCmd.Remove(card);
        }
    }

    private static SequencedSkillCard AddSequencedCard(
        Player player,
        List<string> playOrder,
        string name,
        CardPilePosition position)
    {
        var card = (SequencedSkillCard)new SequencedSkillCard(playOrder, name).MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Draw, position);
        return card;
    }

    private static TCard AddTo<TCard>(Player player, PileType pileType, CardPilePosition position = CardPilePosition.Bottom)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType, position);
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

    private sealed class SequencedSkillCard(List<string> playOrder, string name) : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 0;

        protected override Task OnPlay(CardPlay cardPlay)
        {
            playOrder.Add(name);
            return Task.CompletedTask;
        }
    }

    private sealed class AutoPlayResourceProbeCard : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Token;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 2;

        protected override int CanonicalStarCost => 1;

        public CardPlay? LastPlay { get; private set; }

        protected override Task OnPlay(CardPlay cardPlay)
        {
            LastPlay = cardPlay;
            return Task.CompletedTask;
        }
    }
}
