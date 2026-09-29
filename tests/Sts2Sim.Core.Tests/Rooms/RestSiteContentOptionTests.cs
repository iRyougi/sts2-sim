using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public sealed class RestSiteContentOptionTests : IDisposable
{
    public RestSiteContentOptionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("clone", "PaelsGrowth")]
    [InlineData("kindle", "PumpkinCandle")]
    [InlineData("dig", "Shovel")]
    [InlineData("lift", "Girya")]
    public async Task ContentOption_ResolvesItsEffectThroughEnabledSnapshot(string optionId, string relicName)
    {
        var run = new RunState("rest-content-" + optionId, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        Type? relicType = ContentRegistry.AllTypes.SingleOrDefault(type => type.Name == relicName);
        Assert.NotNull(relicType);
        await RelicCmd.Obtain((RelicModel)ModelDb.Get(relicType), player);
        var room = new RestSiteRoom();

        CardModel? marked = null;
        if (optionId == "clone")
        {
            marked = player.Deck.Cards.Single(card => card.Enchantments.Any(enchantment => enchantment is Clone));
            CardCmd.Upgrade(marked);
            await CardCmd.Enchant<Clone>(player.Deck.Cards.First(card => card.Enchantments.Count == 0), 4m);
        }
        if (optionId == "kindle")
        {
            await player.Relics.OfType<PumpkinCandle>().Single().AfterCombatEnd();
        }
        if (optionId == "dig")
        {
            foreach (RelicRarity rarity in Enum.GetValues<RelicRarity>())
                while (player.RelicGrabBag.PullFromFront(rarity, run, relic => relic is not Strawberry) is not null) { }
            // Leave only Strawberry and position the real stream immediately before a common roll.
            while (player.PlayerRng.Rewards.CloneExact().NextFloat() >= 0.5f)
                player.PlayerRng.Rewards.NextFloat();
        }

        RestSiteDecision decision = Assert.Single(room.GetAvailableDecisions(run, player), item => item.OptionId == optionId);
        var staleSmith = new RestSiteDecision.Smith(player.Deck.Cards.First(card => card.IsUpgradable && card.Enchantments.Count == 0));
        CardCmd.Upgrade(staleSmith.Card);
        RestSiteDecision[] snapshot = [staleSmith, decision];
        Assert.Same(decision, RestSiteDecisionPolicy.ChooseDefault(snapshot));
        Assert.False(RestSiteDecisionPolicy.Contains(snapshot, staleSmith));
        Assert.Empty(RestSiteDecisionCandidates.Build(player, [staleSmith]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => room.ResolveAsync(player, staleSmith));
        var observation = ObservationEncoder.EncodeRestSiteDecision(run, player, snapshot);
        var candidate = Assert.Single(observation.Candidates);
        Assert.True(observation.LegalActionMask[candidate.SlotIndex]);
        Assert.Same(decision, ActionDecoder.DecodeRestSiteChoice(candidate.SlotIndex, player, snapshot));

        // A content option can cross the original packed capacity without acquiring a Core type branch.
        RestSiteDecision[] expandedSnapshot = Enumerable.Repeat(decision, ActionSpaceLayout.MaxRestSiteChoices + 1).ToArray();
        var expanded = RestSiteDecisionCandidates.Build(player, expandedSnapshot);
        Assert.Equal(1044, expanded[^1].SlotIndex);
        Assert.Same(decision, ActionDecoder.DecodeRestSiteChoice(expanded[^1].SlotIndex, player, expandedSnapshot));
        Assert.True(ObservationEncoder.EncodeRestSiteDecision(run, player, expandedSnapshot).LegalActionMask[1044]);
        Assert.Throws<ArgumentOutOfRangeException>(() => RestSiteDecisionCandidates.Build(player,
            Enumerable.Repeat(decision, ActionSpaceLayout.MaxRestSiteChoices + ActionSpaceLayout.MaxRestSiteOverflowChoices + 1).ToArray()));
        await room.ResolveAsync(player, decision);

        switch (optionId)
        {
            case "clone":
                Assert.Equal(12, player.Deck.Cards.Count);
                Assert.Equal(4, player.Deck.Cards.Count(card => card.Enchantments.Any(enchantment => enchantment is Clone)));
                CardModel copy = Assert.Single(player.Deck.Cards.Skip(10), card => card.Id == marked!.Id && card.CurrentUpgradeLevel == 1);
                Assert.NotSame(marked, copy);
                Assert.Same(player, copy.Owner);
                Assert.NotSame(marked!.Enchantments.Single(), copy.Enchantments.Single());
                Assert.Equal(4m, copy.Enchantments.Single().Magnitude);
                break;
            case "kindle":
                Assert.Equal(9, player.Relics.OfType<PumpkinCandle>().Single().KindleCount);
                Assert.Equal(4m, player.Relics.OfType<PumpkinCandle>().Single().ModifyMaxEnergy(player, 3m));
                break;
            case "dig":
                Strawberry relic = Assert.Single(player.Relics.OfType<Strawberry>());
                Assert.True(relic.IsMutable);
                Assert.Same(player, relic.Owner);
                Assert.Equal(1, relic.StackCount);
                Assert.Equal(82, player.Creature.MaxHp);
                Assert.False(player.RelicGrabBag.HasAvailableRelics());
                break;
            case "lift":
                Assert.Equal(1, player.Relics.OfType<Girya>().Single().TimesLifted);
                Assert.Empty(room.GetAvailableDecisions(run, player));
                for (int remaining = 0; remaining < 2; remaining++)
                {
                    await room.Exit(run);
                    room = new RestSiteRoom();
                    await room.EnterInternal(run);
                    await room.ResolveAsync(player, Assert.Single(room.GetAvailableDecisions(run, player), item => item.OptionId == "lift"));
                    Assert.Empty(room.GetAvailableDecisions(run, player));
                }
                await room.Exit(run);
                room = new RestSiteRoom();
                Assert.DoesNotContain(room.GetAvailableDecisions(run, player), item => item.OptionId == "lift");
                var combat = new CombatRoom(() => (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone());
                await combat.EnterInternal(run);
                await Sts2Sim.Core.Hooks.Hook.AfterRoomEntered(run, combat);
                Assert.Equal(3, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
                break;
        }
    }
}
