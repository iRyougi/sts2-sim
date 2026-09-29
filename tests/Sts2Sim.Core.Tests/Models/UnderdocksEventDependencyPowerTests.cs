using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class UnderdocksEventDependencyPowerTests : IDisposable
{
    public UnderdocksEventDependencyPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(FeedingFrenzyPower), typeof(HelloWorldPower), typeof(ReboundPower),
            typeof(SnapshotMutationRelic),
        }).Distinct());
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("FeedingFrenzyPower")]
    [InlineData("HelloWorldPower")]
    [InlineData("ReboundPower")]
    public async Task EventDependencyPowers_PreserveTheirCrossTurnLifecycle(string powerName)
    {
        var run = new RunState("underdocks-dependency-powers", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var combat = new CombatState(run);
        Creature enemy = combat.AddMonster((MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(), CombatSide.Enemy);
        var engine = new CombatEngine(combat);
        await engine.StartCombatAsync();
        await CreatureCmd.SetMaxAndCurrentHp(enemy, 100);
        player.PlayerCombatState!.Energy = 20;

        if (powerName == "FeedingFrenzyPower")
        {
            await PowerCmd.Apply<StrengthPower>(combat, player.Creature, 2m, player.Creature, null);
            await PowerCmd.Apply<FeedingFrenzyPower>(combat, player.Creature, 5m, player.Creature, null);
            Assert.Equal(7, player.Creature.GetPower<StrengthPower>()!.Amount);
            Assert.Equal(5, player.Creature.GetPower<FeedingFrenzyPower>()!.Amount);
            await PowerCmd.Apply<FeedingFrenzyPower>(combat, player.Creature, 2m, player.Creature, null);
            Assert.Equal(9, player.Creature.GetPower<StrengthPower>()!.Amount);
            Assert.Equal(7, player.Creature.GetPower<FeedingFrenzyPower>()!.Amount);
            await CreatureCmd.Damage(combat, new[] { enemy }, 4m, ValueProp.Move, player.Creature, null, null);
            Assert.Equal(87, enemy.CurrentHp);
            await engine.EndPlayerTurnAsync();
            Assert.Null(player.Creature.GetPower<FeedingFrenzyPower>());
            Assert.Equal(2, player.Creature.GetPower<StrengthPower>()!.Amount);
            await CreatureCmd.Damage(combat, new[] { enemy }, 4m, ValueProp.Move, player.Creature, null, null);
            Assert.Equal(81, enemy.CurrentHp);
            await PowerCmd.Apply<FeedingFrenzyPower>(combat, player.Creature, 3m, player.Creature, null);
            await PowerCmd.ModifyAmount(combat, player.Creature.GetPower<StrengthPower>()!, -2m, null, null);
            await engine.EndPlayerTurnAsync();
            Assert.Null(player.Creature.GetPower<FeedingFrenzyPower>());
            Assert.Null(player.Creature.GetPower<StrengthPower>());
            return;
        }

        if (powerName == "HelloWorldPower")
        {
            foreach (CardModel card in player.PlayerCombatState.AllPiles.SelectMany(pile => pile.Cards).ToArray())
                CardPileCmd.Remove(card);
            await PowerCmd.Apply<HelloWorldPower>(combat, player.Creature, 1m, player.Creature, null);
            HelloWorldPower power = player.Creature.GetPower<HelloWorldPower>()!;
            await Hook.BeforeHandDraw(combat, player);
            Assert.Empty(player.PlayerCombatState.Hand.Cards);
            await RelicCmd.Obtain(ModelDb.Relic<SnapshotMutationRelic>(), player);
            SnapshotMutationRelic relic = player.Relics.OfType<SnapshotMutationRelic>().Single();
            relic.AmountAtNextTurn = 3;
            await engine.EndPlayerTurnAsync();
            Assert.Equal(1, power.AmountOnTurnStart);
            Assert.Equal(3, power.Amount);
            CardModel first = Assert.Single(player.PlayerCombatState.Hand.Cards);
            Assert.Equal(CardRarity.Common, first.Rarity);
            Assert.Same(player, first.Owner);
            CardPileCmd.Remove(first);
            await engine.EndPlayerTurnAsync();
            Assert.Equal(3, power.AmountOnTurnStart);
            Assert.Equal(3, player.PlayerCombatState.Hand.Cards.Count);
            Assert.Equal(3, player.PlayerCombatState.Hand.Cards.Select(card => card.Id).Distinct().Count());
            Assert.All(player.PlayerCombatState.Hand.Cards, card =>
            {
                Assert.Equal(CardRarity.Common, card.Rarity);
                Assert.Same(player, card.Owner);
                Assert.False(card.IsUpgraded);
            });
            return;
        }

        if (powerName == "ReboundPower")
        {
            await PowerCmd.Apply<ReboundPower>(combat, player.Creature, 2m, player.Creature, null);
            DefendRegent exhausted = Add<DefendRegent>(player);
            exhausted.AddKeywordInternal(CardKeyword.Exhaust);
            await engine.PlayCardAsync(player, exhausted, null);
            Assert.Same(player.PlayerCombatState.ExhaustPile, exhausted.Pile);
            Assert.Equal(2, player.Creature.GetPower<ReboundPower>()!.Amount);
            DefendRegent first = Add<DefendRegent>(player);
            await engine.PlayCardAsync(player, first, null);
            Assert.Same(first, player.PlayerCombatState.DrawPile.Cards[0]);
            Assert.Equal(1, player.Creature.GetPower<ReboundPower>()!.Amount);
            DefendRegent second = Add<DefendRegent>(player);
            await engine.PlayCardAsync(player, second, null);
            Assert.Same(second, player.PlayerCombatState.DrawPile.Cards[0]);
            Assert.Null(player.Creature.GetPower<ReboundPower>());
            DefendRegent third = Add<DefendRegent>(player);
            await engine.PlayCardAsync(player, third, null);
            Assert.Same(player.PlayerCombatState.DiscardPile, third.Pile);
            await PowerCmd.Apply<ReboundPower>(combat, player.Creature, 1m, player.Creature, null);
            await engine.EndPlayerTurnAsync();
            Assert.Null(player.Creature.GetPower<ReboundPower>());
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(powerName));
    }

    private static T Add<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    public sealed class SnapshotMutationRelic : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Event;
        public int AmountAtNextTurn { get; set; }

        public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
        {
            if (AmountAtNextTurn > 0 && participants.Contains(Owner.Creature))
            {
                Owner.Creature.GetPower<HelloWorldPower>()!.SetAmount(AmountAtNextTurn);
                AmountAtNextTurn = 0;
            }
            return Task.CompletedTask;
        }
    }
}
