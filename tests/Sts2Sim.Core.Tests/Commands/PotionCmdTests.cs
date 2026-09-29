using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Commands;

file sealed class SpyPotion : PotionModel
{
    public bool WasUsed { get; private set; }

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.Self;

    protected override Task OnUse(Creature? target)
    {
        WasUsed = true;
        return Task.CompletedTask;
    }
}

[Collection("ModelDb")]
public class PotionCmdTests : IDisposable
{
    public PotionCmdTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
            typeof(SpyPotion), typeof(StrengthPotion), typeof(Sts2Sim.Core.Models.Powers.StrengthPower), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void NewPlayer_HasThreeEmptyPotionSlots()
    {
        var runState = new RunState("potion-cmd-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);

        Assert.Equal(3, player.MaxPotionCount);
        Assert.All(player.PotionSlots, slot => Assert.Null(slot));
    }

    [Fact]
    public async Task Use_CombatOnlyPotion_InCombat_InvokesOnUseAndClearsSlot()
    {
        var runState = new RunState("potion-cmd-b", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);

        var potion = (SpyPotion)ModelDb.Potion<SpyPotion>().MutableClone();
        player.AddPotionInternal(potion);

        await PotionCmd.Use(potion, player, player.Creature);

        Assert.True(potion.WasUsed);
        Assert.DoesNotContain(potion, player.PotionSlots);
    }

    [Fact]
    public async Task Use_CombatOnlyPotion_OutOfCombat_Throws()
    {
        var runState = new RunState("potion-cmd-c", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var potion = (SpyPotion)ModelDb.Potion<SpyPotion>().MutableClone();
        player.AddPotionInternal(potion);

        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, player, target: null));
    }

    [Fact]
    public void AddPotionInternal_ClonesCanonicalAndAssignsOwner()
    {
        var runState = new RunState("potion-cmd-owner", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        PotionModel canonical = ModelDb.Potion<SpyPotion>();

        PotionModel owned = player.AddPotionInternal(canonical);

        Assert.NotSame(canonical, owned);
        Assert.Same(player, owned.Owner);
        Assert.Contains(owned, player.PotionSlots);
    }

    [Fact]
    public async Task Use_RejectsPotionOwnedByAnotherPlayerOrAlreadyConsumed()
    {
        var runState = new RunState("potion-cmd-membership", new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(other);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        var potion = (SpyPotion)ModelDb.Potion<SpyPotion>().MutableClone();
        owner.AddPotionInternal(potion);
        Assert.Throws<InvalidOperationException>(() => owner.AddPotionInternal(potion));

        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, other, target: null));
        await PotionCmd.Use(potion, owner, owner.Creature);
        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, owner, target: null));
    }

    [Fact]
    public async Task Use_InvalidSelfTargetsDoNotConsumePotion()
    {
        var runState = new RunState("potion-cmd-target", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        var potion = (SpyPotion)player.AddPotionInternal(ModelDb.Potion<SpyPotion>());

        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, player, target: null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, player, room.Engine.State.Enemies[0]));

        Assert.Contains(potion, player.PotionSlots);
        Assert.False(potion.WasUsed);
    }

    [Fact]
    public async Task Use_ForeignCombatTargetDoesNotConsumePotion()
    {
        var ownerRun = new RunState("potion-cmd-owner-combat", new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), ownerRun);
        ownerRun.AddPlayer(owner);
        var ownerRoom = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await ownerRoom.Enter(ownerRun);

        var foreignRun = new RunState("potion-cmd-foreign-combat", new Overgrowth());
        Player foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), foreignRun);
        foreignRun.AddPlayer(foreign);
        var foreignRoom = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await foreignRoom.Enter(foreignRun);

        PotionModel potion = owner.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, owner, foreign.Creature));

        Assert.Contains(potion, owner.PotionSlots);
        Assert.Empty(foreign.Creature.Powers);
    }

    [Fact]
    public async Task Use_AfterCombatExitIsRejectedWithoutConsumingPotion()
    {
        var runState = new RunState("potion-cmd-after-exit", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        PotionModel potion = player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        await room.Exit(runState);

        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.Use(potion, player, player.Creature));

        Assert.Contains(potion, player.PotionSlots);
    }
}
