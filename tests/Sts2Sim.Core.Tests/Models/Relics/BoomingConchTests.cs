using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class BoomingConchTests : IDisposable
{
    public BoomingConchTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EliteFirstTurn_ModifiesOnlyOwnerDrawAndEnergyWithParticipantBoundary()
    {
        RunState runState = CreateRun("booming-conch", out Player owner, out Player foreign);
        await RelicCmd.Obtain(ModelDb.Relic<BoomingConch>(), owner);
        runState.PushRoom(CreateCombatRoom(RoomType.Elite));
        CombatState combatState = CreateCombatState(runState, owner, foreign);
        owner.PlayerCombatState!.TurnNumber = 1;
        foreign.PlayerCombatState!.TurnNumber = 1;

        Assert.Equal(7m, Hook.ModifyHandDraw(combatState, owner, 5m));
        Assert.Equal(5m, Hook.ModifyHandDraw(combatState, foreign, 5m));

        await Hook.AfterSideTurnStart(
            combatState,
            CombatSide.Enemy,
            new Creature[] { foreign.Creature });
        Assert.Equal(0, owner.PlayerCombatState.Energy);

        await Hook.AfterSideTurnStart(
            combatState,
            CombatSide.Enemy,
            new Creature[] { owner.Creature });
        Assert.Equal(1, owner.PlayerCombatState.Energy);

        owner.PlayerCombatState.TurnNumber = 2;
        Assert.Equal(5m, Hook.ModifyHandDraw(combatState, owner, 5m));
        await Hook.AfterSideTurnStart(
            combatState,
            CombatSide.Player,
            new Creature[] { owner.Creature });
        Assert.Equal(1, owner.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task NonEliteRoom_DoesNotModifyDrawOrEnergy()
    {
        RunState runState = CreateRun("booming-conch-room", out Player owner, out Player foreign);
        await RelicCmd.Obtain(ModelDb.Relic<BoomingConch>(), owner);
        runState.PushRoom(CreateCombatRoom(RoomType.Monster));
        CombatState combatState = CreateCombatState(runState, owner, foreign);
        owner.PlayerCombatState!.TurnNumber = 1;

        Assert.Equal(5m, Hook.ModifyHandDraw(combatState, owner, 5m));
        await Hook.AfterSideTurnStart(
            combatState,
            CombatSide.Player,
            new Creature[] { owner.Creature });
        Assert.Equal(0, owner.PlayerCombatState.Energy);
    }

    [Fact]
    public void Metadata_MatchesAncientOngoingRelic()
    {
        BoomingConch relic = ModelDb.Relic<BoomingConch>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.IsUsedUp);
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

    private static CombatState CreateCombatState(RunState runState, params Player[] players)
    {
        var combatState = new CombatState(runState);
        foreach (Player player in players)
        {
            player.ResetCombatState();
            combatState.AddPlayerCreature(player.Creature);
        }
        return combatState;
    }

    private static CombatRoom CreateCombatRoom(RoomType roomType) =>
        new((Func<MonsterModel>)(() => throw new InvalidOperationException()), roomType);
}