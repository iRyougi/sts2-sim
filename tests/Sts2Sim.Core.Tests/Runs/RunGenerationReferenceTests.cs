using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

public sealed class RunGenerationReferenceTests : IDisposable
{
    public RunGenerationReferenceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    [Fact]
    public void NewRun_InitialEncountersMatchArchivedA10SilentReference()
    {
        const string seed = "8F9CPYQ6QYEN";
        var run = new RunState(seed, ActDefinition.GetRandomList(seed), ascensionLevel: 10);
        run.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), run));

        // Archived run_history.json F2/F3/F5: SLIMES_WEAK, SHRINKER_BEETLE_WEAK, NIBBITS_WEAK.
        Assert.Equal(
            new[] { "SlimesWeak", "ShrinkerBeetle", "LoneNibbit" },
            Enumerable.Range(0, 3).Select(_ => run.PullNextEncounter(RoomType.Monster).Name));
        Assert.Equal(0, run.Rng.TreasureRoomRelics.Counter);
    }

    [Fact]
    public void FirstCombat_ArchivedA10SilentRewardRollOffersPotionAndFifteenGold()
    {
        var run = new RunState("8F9CPYQ6QYEN", ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        // run_history.json F2 records gold_gained=15 and a SWIFT_POTION offer.
        // Neow's Precise Scissors does not consume the player's Rewards stream.
        var rewards = Sts2Sim.Core.Rewards.RewardsSet.GenerateFor(
            player, RoomType.Monster, run, encounter: run.PullNextEncounter(RoomType.Monster));

        Assert.NotNull(rewards.Potion);
        Assert.Equal(15, rewards.Gold.Amount);
    }
    public void Dispose() => ModelDb.ResetForTests();
}
