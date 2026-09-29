using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
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
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Combat;

[Collection("ModelDb")]
public sealed class CombatPredictionSinkTests : IDisposable
{
    public CombatPredictionSinkTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("heal-clamped")]
    [InlineData("lizard-tail")]
    [InlineData("fairy-in-a-bottle")]
    [InlineData("brightest-flame")]
    [InlineData("alchemize-full-slots")]
    [InlineData("alchemize-free-slot")]
    public async Task ReportsPassiveEventsWithoutChangingBehavior(string scenario)
    {
        var withSink = await RunAsync(scenario, attachSink: true);
        var withoutSink = await RunAsync(scenario, attachSink: false);
        Assert.Equal(withoutSink.Digest, withSink.Digest);

        switch (scenario)
        {
            case "heal-clamped":
            {
                var heal = Assert.IsType<HealEvent>(Assert.Single(withSink.Events));
                Assert.Equal(10m, heal.Requested);
                Assert.True(heal.HpAfter - heal.HpBefore < heal.Requested);
                Assert.True(heal.HpAfter > heal.HpBefore);
                Assert.Same(withSink.Player.Creature, heal.Creature);
                Assert.Equal(withSink.Player.Creature.MaxHp, heal.HpAfter);
                break;
            }
            case "lizard-tail":
            case "fairy-in-a-bottle":
            {
                Assert.Collection(withSink.Events,
                    item => Assert.IsType<HealEvent>(item),
                    item =>
                    {
                        var prevented = Assert.IsType<DeathEvent>(item);
                        Assert.Equal(scenario == "lizard-tail" ? nameof(LizardTail) : nameof(FairyInABottle), prevented.Preventer);
                        Assert.Equal(0, prevented.HpBefore);
                        Assert.True(prevented.HpAfter > 0);
                        Assert.Same(withSink.Player.Creature, prevented.Target);
                        Assert.Equal(withSink.Player.Creature.CurrentHp, prevented.HpAfter);
                    });
                break;
            }
            case "brightest-flame":
            {
                var loss = Assert.IsType<MaxHpEvent>(Assert.Single(withSink.Events));
                Assert.Equal(2m, loss.Amount);
                Assert.True(loss.IsFromCard);
                Assert.Equal(2, loss.MaxHpBefore - loss.MaxHpAfter);
                Assert.Same(withSink.Player.Creature, loss.Creature);
                break;
            }
            case "alchemize-full-slots":
                Assert.Empty(withSink.Events);
                break;
            case "alchemize-free-slot":
            {
                var procured = Assert.IsType<PotionEvent>(Assert.Single(withSink.Events));
                Assert.Equal(withSink.ProcuredPotionId, procured.PotionId);
                Assert.Same(withSink.Player, procured.Player);
                Assert.False(withSink.AxeBefore);
                Assert.True(withSink.AxeAfter);
                break;
            }
        }
    }

    private static async Task<(CombatStateDescriptionDigest Digest, IReadOnlyList<object> Events,
        Player Player, string? ProcuredPotionId, bool AxeBefore, bool AxeAfter)> RunAsync(
        string scenario, bool attachSink)
    {
        var run = new RunState($"prediction-sink-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        if (scenario == "lizard-tail")
            await RelicCmd.Obtain(ModelDb.Relic<LizardTail>(), player);
        if (scenario == "fairy-in-a-bottle")
            player.AddPotionInternal((FairyInABottle)ModelDb.Potion<FairyInABottle>().MutableClone());
        if (scenario == "alchemize-free-slot")
        {
            await RelicCmd.Obtain(ModelDb.Relic<ThrowingAxe>(), player);
            while (player.PotionSlots.Count(potion => potion is null) > 1)
                player.AddPotionInternal((FirePotion)ModelDb.Potion<FirePotion>().MutableClone());
        }
        if (scenario == "alchemize-full-slots")
        {
            while (player.PotionSlots.Contains(null))
                player.AddPotionInternal((FirePotion)ModelDb.Potion<FirePotion>().MutableClone());
        }

        var state = new CombatState(run);
        state.AddMonster((TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
        var engine = new CombatEngine(state);
        await engine.StartCombatAsync();
        var sink = new RecordingSink();
        if (attachSink)
        {
            state.PredictionSink = sink;
            Assert.Null(state.Clone().PredictionSink);
        }

        bool axeBefore = player.Relics.OfType<ThrowingAxe>().SingleOrDefault()?.UsedThisCombat ?? false;
        switch (scenario)
        {
            case "heal-clamped":
                await CreatureCmd.Damage(state, [player.Creature], 3m, ValueProp.Unblockable,
                    null, null, null);
                await CreatureCmd.Heal(player.Creature, 10m);
                break;
            case "lizard-tail":
            case "fairy-in-a-bottle":
                await CreatureCmd.Damage(state, [player.Creature], 999m, ValueProp.Unblockable,
                    null, null, null);
                break;
            case "brightest-flame":
            case "alchemize-full-slots":
            case "alchemize-free-slot":
            {
                CardModel card = scenario == "brightest-flame"
                    ? (CardModel)ModelDb.Card<BrightestFlame>().MutableClone()
                    : (CardModel)ModelDb.Card<Alchemize>().MutableClone();
                card.AssignOwner(player);
                CardPileCmd.Add(card, PileType.Hand);
                Assert.NotNull(await engine.PlayCardWithResultAsync(player, card, null));
                break;
            }
        }

        var builder = new CombatStateDescriptionBuilder();
        CombatStateDescription.AppendExactState(ref builder, state);
        return (builder.Build(), sink.Events, player,
            player.PotionSlots.OfType<PotionModel>().LastOrDefault()?.Id.ToString(),
            axeBefore, player.Relics.OfType<ThrowingAxe>().SingleOrDefault()?.UsedThisCombat ?? false);
    }

    private sealed record HealEvent(Creature Creature, decimal Requested, int HpBefore, int HpAfter);
    private sealed record DeathEvent(Creature Target, string Preventer, int HpBefore, int HpAfter);
    private sealed record MaxHpEvent(Creature Creature, decimal Amount, int MaxHpBefore, int MaxHpAfter, bool IsFromCard);
    private sealed record PotionEvent(Player Player, string PotionId);

    private sealed class RecordingSink : ICombatPredictionSink
    {
        public List<object> Events { get; } = [];
        public void HealApplied(Creature creature, decimal requested, int hpBefore, int hpAfter) =>
            Events.Add(new HealEvent(creature, requested, hpBefore, hpAfter));
        public void DeathPrevented(Creature target, AbstractModel preventer, int hpBefore, int hpAfter) =>
            Events.Add(new DeathEvent(target, preventer.GetType().Name, hpBefore, hpAfter));
        public void MaxHpLost(Creature creature, decimal amount, int maxHpBefore, int maxHpAfter, bool isFromCard) =>
            Events.Add(new MaxHpEvent(creature, amount, maxHpBefore, maxHpAfter, isFromCard));
        public void PotionProcured(Player player, PotionModel potion) =>
            Events.Add(new PotionEvent(player, potion.Id.ToString()));
    }
}
