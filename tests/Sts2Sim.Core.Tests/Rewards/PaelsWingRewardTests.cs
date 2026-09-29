using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

[Collection("ModelDb")]
public sealed class PaelsWingRewardTests : IDisposable
{
    public PaelsWingRewardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task TwoSacrifices_ThroughRunDriverAndRl_CompleteMainAndExtraAndGrantOneRelic()
    {
        var run = new RunState("h6-sacrifice-source-lock", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        await RelicCmd.Obtain(ModelDb.Relic<PaelsWing>(), player);
        int deckBefore = player.Deck.Cards.Count;
        int relicsBefore = player.Relics.Count;
        var first = new CardReward(player, new CardModel[] {
            ModelDb.Card<StrikeRegent>(), ModelDb.Card<DefendRegent>(), ModelDb.Card<Venerate>() });
        var second = new CardReward(player, new CardModel[] {
            ModelDb.Card<StrikeRegent>(), ModelDb.Card<DefendRegent>(), ModelDb.Card<Venerate>() });
        first.Populate(run);
        second.Populate(run);
        var rewards = RewardsSet.CreateCustom(player, card: first, extraRewards: [second]);
        var prototype = new SacrificeOfferEvent(rewards);
        var room = new EventRoom(() => (EventModel)prototype.MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        int rngBefore = player.PlayerRng.Rewards.Counter;
        RelicModel expected = player.RelicGrabBag.Clone().PullFromFront(
            RelicFactory.RollRarity(player.PlayerRng.Rewards.CloneExact()), run) ?? ModelDb.Relic<Circlet>();
        var wing = Assert.Single(player.Relics.OfType<PaelsWing>());
        CardRewardAlternative firstAlternative = Assert.Single(first.Alternatives);
        Assert.Throws<InvalidOperationException>(() => { _ = second.SelectAlternative(firstAlternative); });
        Task? reentered = null;
        wing.Flashed += (_, _) =>
        {
            if (!first.IsResolved)
            {
                reentered = first.SelectAlternative(firstAlternative);
                Assert.Throws<InvalidOperationException>(() => { _ = first.Skip(); });
            }
        };
        var decisions = new SacrificeDecisionSource(run, relicsBefore, rngBefore);
        await new RunDriver(run, decisions).DriveEventAsync(room);
        Assert.Equal(2, decisions.Sacrifices);
        Assert.Equal(2, wing.RewardsSacrificed);
        Assert.Equal(0, wing.DisplayAmount);
        Assert.Equal(2, ((PaelsWing)wing.MutableClone()).RewardsSacrificed);
        Assert.Same(reentered, first.SelectAlternative(firstAlternative));
        Assert.Throws<InvalidOperationException>(() => { _ = first.Skip(); });
        Assert.Throws<InvalidOperationException>(() => { _ = first.SelectOption(first.Options[0]); });
        Assert.Equal(expected.Id, player.Relics.Last().Id);
        Assert.NotSame(expected, player.Relics.Last());
        Assert.Same(player, player.Relics.Last().Owner);
        var next = new CardReward(player, Array.Empty<CardModel>());
        next.Populate(run);
        var nextRewards = RewardsSet.CreateCustom(player, card: next);
        await nextRewards.Gold.Take();
        Assert.IsType<RewardDecisionClassification.Choice>(RewardDecisionClassifier.Classify(nextRewards));
        Assert.Equal(0, Assert.Single(ObservationEncoder.EncodeRewardDecision(run, nextRewards).Relics,
            relic => relic.ModelId == wing.Id.ToString()).Counter);
        await next.Skip();
        Assert.Equal(2, wing.RewardsSacrificed);
        Assert.True(first.IsResolved);
        Assert.True(second.IsResolved);
        Assert.Null(first.SelectedOption);
        Assert.Null(second.SelectedOption);
        Assert.Equal(deckBefore, player.Deck.Cards.Count);
        Assert.Equal(relicsBefore + 1, player.Relics.Count);
        Assert.Equal(rngBefore + 1, player.PlayerRng.Rewards.Counter);
    }

    private sealed class SacrificeOfferEvent(RewardsSet rewards) : EventModel
    {
        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
            [new EventOption("OFFER", () => {
                OfferRewards(rewards);
                Finish();
                return Task.CompletedTask;
            })];
    }

    private sealed class SacrificeDecisionSource(RunState run, int relicsBefore, int rngBefore) : IRunDecisionSource
    {
        public int Sacrifices { get; private set; }
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) => Task.FromResult(options[0]);
        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
        public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
        {
            if (RewardDecisionClassifier.Classify(rewards) is RewardDecisionClassification.Automatic automatic)
                return Task.FromResult(automatic.Decision);
            ObservationSnapshot observation = ObservationEncoder.EncodeRewardDecision(run, rewards);
            CandidateSlot slot = Assert.Single(observation.Candidates,
                candidate => candidate.Label == "card_reward_alternative:SACRIFICE");
            Assert.Equal(5, observation.Candidates.Count);
            Assert.Equal(1076, slot.SlotIndex);
            Assert.Equal(62, ActionSpaceLayout.ShopBase);
            Assert.True(observation.LegalActionMask[slot.SlotIndex]);
            Assert.Equal(Sacrifices % 2, Assert.Single(observation.Relics,
                relic => relic.ModelId == ModelDb.Relic<PaelsWing>().Id.ToString()).Counter);
            Assert.Equal(relicsBefore, rewards.Player.Relics.Count);
            Assert.Equal(rngBefore, rewards.Player.PlayerRng.Rewards.Counter);
            Assert.Equal(observation.Candidates, ObservationEncoder.EncodeRewardDecision(run, rewards).Candidates);
            RewardDecision decision = ActionDecoder.DecodeRewardChoice(slot.SlotIndex, rewards);
            Sacrifices++;
            return Task.FromResult(decision);
        }
    }
}
