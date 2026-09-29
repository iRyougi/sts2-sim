using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class SmallCapsuleTests : IDisposable
{
    private sealed class GivesSmallCapsuleAncientEvent : AncientEventModel
    {
        public override IReadOnlyList<RelicModel> AllPossibleOptions =>
            new RelicModel[] { ModelDb.Relic<SmallCapsule>() };

        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
            new[]
            {
                new EventOption("TAKE", async () =>
                {
                    await RelicCmd.Obtain(ModelDb.Relic<SmallCapsule>(), Owner);
                    Finish();
                }),
            };
    }

    public SmallCapsuleTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ProductionEventDrain_ResolvesOnePopulatedRandomRelicReward()
    {
        var runState = new RunState("small-capsule", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new EventRoom(() => (EventModel)new GivesSmallCapsuleAncientEvent().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        int relicCountBefore = player.Relics.Count;

        await RunEngine.DriveEventToCompletion(room);

        RelicModel[] obtained = player.Relics.Skip(relicCountBefore).ToArray();
        Assert.Equal(2, obtained.Length);
        SmallCapsule capsule = Assert.IsType<SmallCapsule>(obtained[0]);
        Assert.Equal(RelicRarity.Ancient, capsule.Rarity);
        Assert.False(capsule.HasUponPickupEffect);
        Assert.NotEqual(RelicRarity.Ancient, obtained[1].Rarity);
        Assert.NotEqual(RelicRarity.Event, obtained[1].Rarity);
        Assert.All(obtained, relic => Assert.Same(player, relic.Owner));
        Assert.False(room.Event.HasPendingRewardOffers);
    }
}
