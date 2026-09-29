using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class FishingRodTests : IDisposable
{
    public FishingRodTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task CombatRoomExit_EveryThirdMonsterUpgradesOneOwnerCardBeforeTeardown()
    {
        RunState runState = CreateRun("fishing-rod-third", out Player owner, out Player foreign);
        await RelicCmd.Obtain(ModelDb.Relic<FishingRod>(), owner);
        FishingRod relic = Assert.Single(owner.Relics.OfType<FishingRod>());
        int nicheBefore = runState.Rng.Niche.Counter;
        int ownerUpgradedBefore = owner.Deck.Cards.Count(card => card.IsUpgraded);
        int foreignUpgradedBefore = foreign.Deck.Cards.Count(card => card.IsUpgraded);

        await EnterAndExitCombat(runState, RoomType.Monster);
        await EnterAndExitCombat(runState, RoomType.Monster);

        Assert.Equal(2, relic.CombatsSeen);
        Assert.Equal(ownerUpgradedBefore, owner.Deck.Cards.Count(card => card.IsUpgraded));
        // Each TrainingDummy spawn consumes one fixed-HP Niche draw.
        Assert.Equal(nicheBefore + 2, runState.Rng.Niche.Counter);

        await EnterAndExitCombat(runState, RoomType.Monster);

        Assert.Equal(3, relic.CombatsSeen);
        Assert.Equal(ownerUpgradedBefore + 1, owner.Deck.Cards.Count(card => card.IsUpgraded));
        Assert.Equal(foreignUpgradedBefore, foreign.Deck.Cards.Count(card => card.IsUpgraded));
        // Three HP draws plus the relic's one upgrade selection.
        Assert.Equal(nicheBefore + 4, runState.Rng.Niche.Counter);
        Assert.Null(owner.PlayerCombatState);
    }

    [Fact]
    public async Task NonMonsterCombat_DoesNotCountOrConsumeAdditionalNicheRng()
    {
        RunState runState = CreateRun("fishing-rod-elite", out Player owner, out _);
        await RelicCmd.Obtain(ModelDb.Relic<FishingRod>(), owner);
        FishingRod relic = Assert.Single(owner.Relics.OfType<FishingRod>());
        int nicheBefore = runState.Rng.Niche.Counter;

        await EnterAndExitCombat(runState, RoomType.Elite);

        Assert.Equal(0, relic.CombatsSeen);
        // Only the TrainingDummy HP draw; no relic selection.
        Assert.Equal(nicheBefore + 1, runState.Rng.Niche.Counter);
    }

    [Fact]
    public async Task ThirdMonsterWithNoUpgradableCards_IsSafeAndDoesNotConsumeAdditionalNicheRng()
    {
        RunState runState = CreateRun("fishing-rod-none", out Player owner, out _);
        await RelicCmd.Obtain(ModelDb.Relic<FishingRod>(), owner);
        foreach (CardModel card in owner.Deck.Cards)
        {
            while (card.IsUpgradable)
            {
                CardCmd.Upgrade(card);
            }
        }
        int nicheBefore = runState.Rng.Niche.Counter;

        await EnterAndExitCombat(runState, RoomType.Monster);
        await EnterAndExitCombat(runState, RoomType.Monster);
        await EnterAndExitCombat(runState, RoomType.Monster);

        FishingRod relic = Assert.Single(owner.Relics.OfType<FishingRod>());
        Assert.Equal(3, relic.CombatsSeen);
        // Three TrainingDummy HP draws; no relic selection.
        Assert.Equal(nicheBefore + 3, runState.Rng.Niche.Counter);
    }

    [Fact]
    public void Metadata_MatchesAncientOngoingRelic()
    {
        FishingRod relic = ModelDb.Relic<FishingRod>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.IsUsedUp);
        Assert.Equal(0, relic.CombatsSeen);
    }

    private static RunState CreateRun(string seed, out Player owner, out Player foreign)
    {
        var runState = new RunState(seed, new Overgrowth());
        owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(foreign);
        return runState;
    }

    private static async Task EnterAndExitCombat(RunState runState, RoomType roomType)
    {
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(),
            roomType);
        runState.PushRoom(room);
        await room.Enter(runState);
        await CreatureCmd.Kill(room.Engine.State.Enemies.Single());
        Assert.True(room.Engine.CheckWinCondition());
        await room.ResolveOutcomeAsync(generateRewards: false);
        await room.Exit(runState);
        Assert.Same(room, runState.PopCurrentRoom());
    }
}