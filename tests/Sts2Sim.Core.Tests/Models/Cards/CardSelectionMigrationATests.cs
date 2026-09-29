using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class CardSelectionMigrationATests : IDisposable
{
    public CardSelectionMigrationATests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("Purity", 0, 3)]
    [InlineData("Begone", 1, 1)]
    [InlineData("Charge", 2, 2)]
    [InlineData("CosmicIndifference", 1, 1)]
    [InlineData("Glimmer", 1, 1)]
    [InlineData("PhotonCut", 1, 1)]
    [InlineData("SeekerStrike", 1, 1)]
    [InlineData("ThinkingAhead", 1, 1)]
    [InlineData("EntropyPower", 2, 2)]
    [InlineData("LiquidMemories", 1, 1)]
    [InlineData("TouchOfInsanity", 1, 1)]
    [InlineData("ToastyMittens", 1, 1)]
    [InlineData("SecretWeapon", 1, 1)]
    [InlineData("SecretTechnique", 1, 1)]
    [InlineData("DecisionsDecisions", 1, 1)]
    [InlineData("HeirloomHammer", 1, 1)]
    [InlineData("DropletOfPrecognition", 1, 1)]
    public async Task PileSelection_OffersCorrectCandidatesAndUsesChosenCards(string effect, int min, int max)
    {
        var run = new RunState("migration-a-" + effect, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        var state = room.Engine.State;
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray()) CardPileCmd.Remove(card);
        foreach (CardModel card in player.PlayerCombatState.DrawPile.Cards.ToArray()) CardPileCmd.Remove(card);
        CardModel[] hand = Enumerable.Range(0, 4).Select(_ => Add<StrikeRegent>(player, PileType.Hand)).ToArray();
        CardModel[] draw = Enumerable.Range(0, 6).Select(_ => effect == "SecretWeapon"
            ? (CardModel)Add<StrikeRegent>(player, PileType.Draw) : Add<DefendRegent>(player, PileType.Draw)).ToArray();
        if (effect == "HeirloomHammer")
        {
            foreach (CardModel card in hand) CardPileCmd.Remove(card);
            hand = Enumerable.Range(0, 3).Select(_ => (CardModel)Add<Purity>(player, PileType.Hand)).ToArray();
        }
        CardModel[] discard = Enumerable.Range(0, 3).Select(_ => Add<StrikeRegent>(player, PileType.Discard)).ToArray();
        var source = new RecordingSource();
        state.CardSelectionSource = source;
        AbstractModel model;
        CardModel[] expected;
        if (effect == "EntropyPower")
        {
            var power = (EntropyPower)ModelDb.Power<EntropyPower>().MutableClone();
            power.ApplyInternal(player.Creature, 2);
            model = power;
            expected = hand;
            await power.AfterSideTurnStart(CombatSide.Player, new[] { player.Creature });
        }
        else if (effect == "ToastyMittens")
        {
            var relic = (ToastyMittens)ModelDb.Relic<ToastyMittens>().MutableClone();
            relic.AssignOwner(player);
            model = relic;
            expected = hand;
            await relic.AfterSideTurnStart(CombatSide.Player, new[] { player.Creature });
        }
        else if (effect is "LiquidMemories" or "TouchOfInsanity" or "DropletOfPrecognition")
        {
            PotionModel potion = effect == "DropletOfPrecognition" ? (PotionModel)ModelDb.Potion<DropletOfPrecognition>().MutableClone() : effect == "LiquidMemories"
                ? (PotionModel)ModelDb.Potion<LiquidMemories>().MutableClone()
                : (PotionModel)ModelDb.Potion<TouchOfInsanity>().MutableClone();
            potion.AssignOwner(player);
            model = potion;
            expected = effect == "DropletOfPrecognition" ? draw : effect == "LiquidMemories" ? discard : hand;
            await potion.UseInternal(player.Creature);
        }
        else
        {
            CardModel card = (CardModel)ModelDb.All<CardModel>().Single(c => c.GetType().Name == effect).MutableClone();
            card.AssignOwner(player);
            CardPileCmd.Add(card, PileType.Hand);
            model = card;
            expected = effect switch
            {
                "Charge" or "SecretWeapon" or "SecretTechnique" => draw,
                "DecisionsDecisions" => draw.Take(3).ToArray(),
                "CosmicIndifference" => discard,
                "Glimmer" => hand.Concat(draw.Take(3)).ToArray(),
                "PhotonCut" => hand.Concat(draw.Take(1)).ToArray(),
                "ThinkingAhead" => hand.Concat(draw.Take(2)).ToArray(),
                "SeekerStrike" => draw.ToList().StableShuffle(new RunState("migration-a-" + effect, new Overgrowth()).Rng.CombatCardSelection).Take(3).ToArray(),
                _ => hand,
            };
            // Combat-pile filtering preserves the original draw-pile order after random subsetting.
            if (effect == "SeekerStrike") expected = draw.Where(card => expected.Any(option => ReferenceEquals(option, card))).ToArray();
            await card.PlayAsync(card.TargetType == TargetType.AnyEnemy ? state.HittableEnemies.Single() : null);
        }
        CardSelectionRequest request = Assert.Single(source.Requests);
        Assert.Same(model, request.Source);
        Assert.Same(player, request.Player);
        Assert.Equal(min, request.MinCount);
        Assert.Equal(max, request.MaxCount);
        Assert.False(request.Cancelable);
        Assert.True(expected.SequenceEqual(request.Candidates, ReferenceEqualityComparer.Instance));
        Assert.Equal(request.Candidates.Count, request.Candidates.Distinct(ReferenceEqualityComparer.Instance).Count());
        if (effect is "CosmicIndifference" or "Glimmer" or "PhotonCut" or "ThinkingAhead")
            Assert.Same(source.Selected!.First(), player.PlayerCombatState.DrawPile.Cards.First());
        if (effect is "Purity" or "ToastyMittens")
            Assert.All(source.Selected!, card => Assert.Contains(player.PlayerCombatState.ExhaustPile.Cards, item => ReferenceEquals(item, card)));
        if (effect is "LiquidMemories" or "SeekerStrike" or "SecretWeapon" or "SecretTechnique" or "DropletOfPrecognition")
            Assert.Contains(player.PlayerCombatState.Hand.Cards, item => ReferenceEquals(item, source.Selected!.Single()));
        if (effect == "TouchOfInsanity") Assert.True(source.Selected!.Single().TemporaryFreeThisCombat);
        if (effect is "Begone" or "EntropyPower")
            Assert.All(source.Selected!, card => Assert.DoesNotContain(player.PlayerCombatState.Hand.Cards, item => ReferenceEquals(item, card)));
        if (effect == "Charge")
            Assert.All(source.Selected!, card => Assert.DoesNotContain(player.PlayerCombatState.DrawPile.Cards, item => ReferenceEquals(item, card)));
    }

    // 原版 CardSelectCmd.FromHand / FromHandForDiscard 开头：CombatManager.IsOverOrEnding 时返回空。
    // 打死最后一个敌人的牌不再弹出后续的"从手牌选"，战斗随即判胜。
    [Theory]
    [InlineData("DaggerThrow", true, 0)]
    [InlineData("DaggerThrow", false, 1)]
    [InlineData("PhotonCut", true, 0)]
    public async Task HandSelection_IsSkippedOnceTheCardEndsCombat(string effect, bool killsLastEnemy, int expectedRequests)
    {
        var run = new RunState("migration-a-ending-" + effect + killsLastEnemy, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        var state = room.Engine.State;
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray()) CardPileCmd.Remove(card);
        foreach (CardModel card in player.PlayerCombatState.DrawPile.Cards.ToArray()) CardPileCmd.Remove(card);
        CardModel[] hand = Enumerable.Range(0, 3).Select(_ => (CardModel)Add<StrikeRegent>(player, PileType.Hand)).ToArray();
        Enumerable.Range(0, 3).Select(_ => Add<DefendRegent>(player, PileType.Draw)).ToArray();
        Creature enemy = state.HittableEnemies.Single();
        if (killsLastEnemy)
        {
            enemy.LoseHpInternal(enemy.CurrentHp - 1, Sts2Sim.Core.ValueProps.ValueProp.Unpowered);
        }

        var source = new RecordingSource();
        state.CardSelectionSource = source;
        CardModel played = (CardModel)ModelDb.All<CardModel>().Single(c => c.GetType().Name == effect).MutableClone();
        played.AssignOwner(player);
        CardPileCmd.Add(played, PileType.Hand);

        await played.PlayAsync(enemy);

        Assert.Equal(expectedRequests, source.Requests.Count);
        Assert.Equal(killsLastEnemy, state.IsOverOrEnding());
        if (killsLastEnemy)
        {
            // CardPileCmd.DrawInternal 同样在 IsOverOrEnding 时返回：打死最后一个敌人后不再抽牌。
            Assert.Equal(3, player.PlayerCombatState.DrawPile.Cards.Count);
            Assert.All(hand, card => Assert.Contains(player.PlayerCombatState.Hand.Cards, item => ReferenceEquals(item, card)));
        }
    }

    private static T Add<T>(Player player, PileType pile) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }

    private sealed class RecordingSource : ICardSelectionDecisionSource
    {
        public List<CardSelectionRequest> Requests { get; } = new();
        public IReadOnlyList<CardModel>? Selected { get; private set; }
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Requests.Add(request);
            Selected = request.Candidates.TakeLast(request.MaxCount).ToArray();
            return Task.FromResult(Selected);
        }
    }
}

