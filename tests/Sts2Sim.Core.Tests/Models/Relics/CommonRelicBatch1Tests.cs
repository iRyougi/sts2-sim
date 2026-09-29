using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

file sealed class CombatEndProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public int CallCount { get; private set; }

    public bool SawCombatStateBeforeCleanup { get; private set; }

    public override Task AfterCombatEnd()
    {
        CallCount++;
        SawCombatStateBeforeCleanup |= Owner.Creature.CombatState is not null &&
                                      Owner.PlayerCombatState is not null;
        return Task.CompletedTask;
    }
}

file sealed class ThrowingCombatEndProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterCombatEnd() =>
        Task.FromException(new InvalidOperationException("combat-end probe"));
}

[Collection("ModelDb")]
public sealed class CommonRelicBatch1Tests : IDisposable
{
    public CommonRelicBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(CombatEndProbeRelic))
                .Append(typeof(ThrowingCombatEndProbeRelic)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task BookOfFiveRings_TracksBothPermanentAcquisitionEntrypoints_ButNotDeckUpgrade()
    {
        (RunState runState, Player player) = CreateRun("book-of-five-rings");
        await RelicCmd.Obtain(ModelDb.Relic<BookOfFiveRings>(), player);
        player.Creature.LoseHpInternal(25m, ValueProp.Unpowered);
        int woundedHp = player.Creature.CurrentHp;

        for (int i = 0; i < 4; i++)
        {
            var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);
            reward.Populate(runState);
            await reward.SelectOption(Assert.Single(reward.Options.Take(1)));
        }

        CardModel deckCard = Assert.Single(player.Deck.Cards.Where(card => card.IsUpgradable).Take(1));
        CardCmd.Upgrade(deckCard);
        Assert.Equal(woundedHp, player.Creature.CurrentHp);

        player.Gold = 99_999;
        var merchant = new MerchantRoom();
        await merchant.EnterInternal(runState);
        await merchant.Buy(merchant.Inventory.Cards[0], player);

        Assert.Equal(Math.Min(woundedHp + 20, player.Creature.MaxHp), player.Creature.CurrentHp);
    }

    [Fact]
    public async Task BookOfFiveRings_DoesNotReviveItsDeadOwner()
    {
        (RunState runState, Player player) = CreateRun("book-of-five-rings-dead");
        await RelicCmd.Obtain(ModelDb.Relic<BookOfFiveRings>(), player);
        player.Creature.LoseHpInternal(player.Creature.CurrentHp, ValueProp.Unpowered);
        Assert.True(player.Creature.IsDead);

        for (int i = 0; i < 5; i++)
        {
            CardModel card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();
            card.AssignOwner(player);
            await CardPileCmd.AddToDeck(card, ModelDb.Card<StrikeRegent>());
        }

        Assert.True(player.Creature.IsDead);
        Assert.Equal(0, player.Creature.CurrentHp);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CombatRoomExit_DispatchesEndOnlyForVictoryButCleansUpEitherOutcome(bool win)
    {
        (RunState runState, Player player) = CreateRun($"combat-end-{win}");
        await RelicCmd.Obtain(ModelDb.Relic<CombatEndProbeRelic>(), player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.EnterInternal(runState);

        if (win)
        {
            await CreatureCmd.Damage(
                room.Engine.State,
                room.Engine.State.Enemies,
                999m,
                ValueProp.Unblockable | ValueProp.Unpowered,
                player.Creature,
                null,
                null);
        }
        else
        {
            await CreatureCmd.Damage(
                room.Engine.State,
                new[] { player.Creature },
                999m,
                ValueProp.Unblockable | ValueProp.Unpowered,
                room.Engine.State.Enemies[0],
                null,
                null);
        }

        Assert.True(room.Engine.CheckWinCondition());
        Assert.Equal(win, room.Engine.Won);

        await Task.WhenAll(room.Exit(runState), room.Exit(runState));
        await room.Exit(runState);

        var probe = Assert.Single(player.Relics.OfType<CombatEndProbeRelic>());
        Assert.Equal(win ? 1 : 0, probe.CallCount);
        Assert.Equal(win, probe.SawCombatStateBeforeCleanup);
        Assert.Null(player.Creature.CombatState);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task CombatRoomExit_CleansUpPlayers_WhenAfterCombatEndThrows()
    {
        (RunState runState, Player player) = CreateRun("combat-end-throws");
        await RelicCmd.Obtain(ModelDb.Relic<ThrowingCombatEndProbeRelic>(), player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.EnterInternal(runState);

        await CreatureCmd.Kill(room.Engine.State.Enemies.Single());
        Assert.True(room.Engine.CheckWinCondition());
        await Assert.ThrowsAsync<InvalidOperationException>(() => room.Exit(runState));

        Assert.Null(player.Creature.CombatState);
        Assert.Null(player.PlayerCombatState);
    }

    [Fact]
    public async Task BronzeScales_AppliesThreeThorns_ThatDamageTheAttacker()
    {
        (RunState runState, Player player) = CreateRun("bronze-scales");
        await RelicCmd.Obtain(ModelDb.Relic<BronzeScales>(), player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);

        ThornsPower thorns = Assert.Single(player.Creature.Powers.OfType<ThornsPower>());
        Assert.Equal(3, thorns.Amount);
        int enemyHpBefore = room.Engine.State.Enemies[0].CurrentHp;

        await DamageCmd.Attack(1m)
            .FromMonster(room.Engine.State.Enemies[0].Monster!)
            .Execute();

        Assert.Equal(enemyHpBefore - 3, room.Engine.State.Enemies[0].CurrentHp);
    }

    [Fact]
    public async Task CentennialPuzzle_DrawsOnlyOnFirstUnblockedHit_AndResetsOnCombatExit()
    {
        (RunState runState, Player player) = CreateRun("centennial-puzzle");
        await RelicCmd.Obtain(ModelDb.Relic<CentennialPuzzle>(), player);

        var firstRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await firstRoom.Enter(runState);
        Assert.Equal(5, player.PlayerCombatState!.Hand.Cards.Count);

        await DamageCmd.Attack(1m).FromMonster(firstRoom.Engine.State.Enemies[0].Monster!).Execute();
        Assert.Equal(8, player.PlayerCombatState.Hand.Cards.Count);
        await DamageCmd.Attack(1m).FromMonster(firstRoom.Engine.State.Enemies[0].Monster!).Execute();
        Assert.Equal(8, player.PlayerCombatState.Hand.Cards.Count);

        await CreatureCmd.Kill(firstRoom.Engine.State.Enemies.Single());
        Assert.True(firstRoom.Engine.CheckWinCondition());
        await firstRoom.ResolveOutcomeAsync(generateRewards: false);
        await firstRoom.Exit(runState);
        var secondRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await secondRoom.Enter(runState);
        await DamageCmd.Attack(1m).FromMonster(secondRoom.Engine.State.Enemies[0].Monster!).Execute();

        Assert.Equal(8, player.PlayerCombatState!.Hand.Cards.Count);

        // The first relic draw kills the only enemy through a real draw hook.
        // Separate Draw(1) calls must stop at the next call's combat-end guard.
        (RunState endingRun, Player endingPlayer) = CreateRun("centennial-ending-draw");
        await RelicCmd.Obtain(ModelDb.Relic<CentennialPuzzle>(), endingPlayer);
        var endingRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await endingRoom.Enter(endingRun);
        foreach (CardModel card in endingPlayer.PlayerCombatState!.DrawPile.Cards.ToArray())
            CardPileCmd.Add(card, PileType.Discard);
        for (int i = 0; i < 3; i++)
        {
            CardModel card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();
            card.AssignOwner(endingPlayer);
            CardPileCmd.Add(card, PileType.Draw);
        }

        await PowerCmd.Apply<SpeedsterPower>(
            endingRoom.Engine.State, endingPlayer.Creature, 2m, endingPlayer.Creature, null);
        endingRoom.Engine.State.Enemies.Single().SetCurrentHpInternal(1m);
        int handBefore = endingPlayer.PlayerCombatState.Hand.Cards.Count;

        await DamageCmd.Attack(1m)
            .FromMonster(endingRoom.Engine.State.Enemies.Single().Monster!)
            .Execute();

        Assert.True(endingRoom.Engine.IsOverOrEnding);
        Assert.Equal(handBefore + 1, endingPlayer.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(2, endingPlayer.PlayerCombatState.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task OpeningRelics_ApplyTheirDistinctFirstTurnEffects()
    {
        (RunState runState, Player player) = CreateRun("opening-relics");
        await RelicCmd.Obtain(ModelDb.Relic<Anchor>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<BagOfMarbles>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<BagOfPreparation>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<BloodVial>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<FestivePopper>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<Gorget>(), player);
        player.Creature.LoseHpInternal(5m, ValueProp.Unpowered);
        int hpBefore = player.Creature.CurrentHp;

        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);

        Assert.Equal(10, player.Creature.Block);
        Assert.Equal(7, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(hpBefore + 2, player.Creature.CurrentHp);
        Assert.Equal(1, Assert.Single(room.Engine.State.Enemies[0].Powers.OfType<VulnerablePower>()).Amount);
        Assert.Equal(9, room.Engine.State.Enemies[0].MaxHp - room.Engine.State.Enemies[0].CurrentHp);
        Assert.Equal(4, Assert.Single(player.Creature.Powers.OfType<PlatingPower>()).Amount);
    }

    [Fact]
    public async Task Anchor_BlockSurvivesFirstPlayerTurn_ButClearsAtSecondPlayerTurn()
    {
        (RunState runState, Player player) = CreateRun("anchor-block-clear");
        await RelicCmd.Obtain(ModelDb.Relic<Anchor>(), player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);

        Assert.Equal(10, player.Creature.Block);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(0, player.Creature.Block);
    }

    [Fact]
    public async Task FencingManual_FirstTurn_ForgesSovereignBladeByTen()
    {
        (RunState runState, Player player) = CreateRun("fencing-manual");
        await RelicCmd.Obtain(ModelDb.Relic<FencingManual>(), player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        SovereignBlade blade = Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<SovereignBlade>());
        int enemyHpBefore = room.Engine.State.Enemies[0].CurrentHp;

        await room.Engine.PlayCardAsync(player, blade, room.Engine.State.Enemies[0]);

        Assert.Equal(enemyHpBefore - 20, room.Engine.State.Enemies[0].CurrentHp);
    }

    [Fact]
    public async Task HappyFlower_GrantsEnergyEveryThirdPlayerTurn_AcrossCombatBoundaries()
    {
        (RunState runState, Player player) = CreateRun("happy-flower");
        await RelicCmd.Obtain(ModelDb.Relic<HappyFlower>(), player);
        var firstRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await firstRoom.Enter(runState);
        await firstRoom.Engine.EndPlayerTurnAsync();
        await CreatureCmd.Kill(firstRoom.Engine.State.Enemies.Single());
        Assert.True(firstRoom.Engine.CheckWinCondition());
        await firstRoom.ResolveOutcomeAsync(generateRewards: false);
        await firstRoom.Exit(runState);

        var secondRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await secondRoom.Enter(runState);

        Assert.Equal(player.MaxEnergy + 1, player.PlayerCombatState!.Energy);
    }

    [Fact]
    public async Task JuzuBracelet_RemovesMonsterFromUnknownRoomCandidates_WithoutMutatingInput()
    {
        (RunState runState, Player player) = CreateRun("juzu-bracelet");
        await RelicCmd.Obtain(ModelDb.Relic<JuzuBracelet>(), player);
        IReadOnlySet<RoomType> original = new HashSet<RoomType>
        {
            RoomType.Monster,
            RoomType.Elite,
            RoomType.Event,
        };

        IReadOnlySet<RoomType> result = Hook.ModifyUnknownMapPointRoomTypes(runState, original);

        Assert.DoesNotContain(RoomType.Monster, result);
        Assert.Contains(RoomType.Elite, result);
        Assert.Contains(RoomType.Event, result);
        Assert.Contains(RoomType.Monster, original);
    }

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
