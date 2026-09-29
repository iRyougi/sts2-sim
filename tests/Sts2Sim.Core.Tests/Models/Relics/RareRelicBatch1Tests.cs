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
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

file sealed class RareBatchAttackCard : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class RareBatchSkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class RareBatchCostlySkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
}

file sealed class RareBatchPowerCard : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class RareBatchDeathProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.None;

    public List<string> Events { get; private set; } = new();

    public override Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner.Creature)
        {
            Events.Add(result.WasTargetKilled ? "damage:lethal" : "damage:nonlethal");
        }

        return Task.CompletedTask;
    }

    public override Task AfterDeath(Creature target)
    {
        if (target == Owner.Creature)
        {
            Events.Add("death");
        }

        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Events = new List<string>(Events);
    }
}

file sealed class RareBatchNonPreventerRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.None;

    public int PreventionCallbacks { get; private set; }

    public override bool ShouldDieLate(Creature creature) => true;

    public override Task AfterPreventingDeath(Creature creature)
    {
        PreventionCallbacks++;
        return Task.CompletedTask;
    }
}

[Collection("ModelDb")]
public sealed class RareRelicBatch1Tests : IDisposable
{
    public RareRelicBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(RareBatchAttackCard))
                .Append(typeof(RareBatchSkillCard))
                .Append(typeof(RareBatchCostlySkillCard))
                .Append(typeof(RareBatchPowerCard))
                .Append(typeof(RareBatchDeathProbeRelic))
                .Append(typeof(RareBatchNonPreventerRelic)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Task7Relics_AreAllRegisteredAsRare()
    {
        RelicModel[] relics =
        {
            ModelDb.Relic<ArtOfWar>(),
            ModelDb.Relic<BeatingRemnant>(),
            ModelDb.Relic<Bellows>(),
            ModelDb.Relic<CaptainsWheel>(),
            ModelDb.Relic<Chandelier>(),
            ModelDb.Relic<CloakClasp>(),
            ModelDb.Relic<GamblingChip>(),
            ModelDb.Relic<GamePiece>(),
            ModelDb.Relic<IceCream>(),
            ModelDb.Relic<IntimidatingHelmet>(),
            ModelDb.Relic<Kunai>(),
            ModelDb.Relic<LizardTail>(),
        };

        Assert.Equal(12, relics.Length);
        Assert.All(relics, relic => Assert.Equal(RelicRarity.Rare, relic.Rarity));
    }

    [Fact]
    public async Task ArtOfWar_GrantsEnergyOnSecondTurnOnlyWhenPriorTurnHadNoAttack()
    {
        (RunState peacefulRun, Player peacefulPlayer) = CreateRun("art-of-war-peaceful");
        await Obtain<ArtOfWar>(peacefulPlayer);
        CombatRoom peacefulRoom = CreateCombatRoom();
        await peacefulRoom.Enter(peacefulRun);

        await peacefulRoom.Engine.EndPlayerTurnAsync();

        Assert.Equal(4, peacefulPlayer.PlayerCombatState!.Energy);

        (RunState attackRun, Player attackPlayer) = CreateRun("art-of-war-attack");
        await Obtain<ArtOfWar>(attackPlayer);
        CombatRoom attackRoom = CreateCombatRoom();
        await attackRoom.Enter(attackRun);
        await attackRoom.Engine.PlayCardAsync(
            attackPlayer,
            AddToHand<RareBatchAttackCard>(attackPlayer),
            attackRoom.Engine.State.Enemies[0]);

        await attackRoom.Engine.EndPlayerTurnAsync();

        Assert.Equal(3, attackPlayer.PlayerCombatState!.Energy);
    }

    [Fact]
    public async Task BeatingRemnant_CapsCumulativeHpLossAtTwentyAndResetsNextTurn()
    {
        (RunState runState, Player player) = CreateRun("beating-remnant");
        await Obtain<BeatingRemnant>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        DamageResult first = Assert.Single(await DamagePlayer(room, player, enemy, 12m));
        DamageResult second = Assert.Single(await DamagePlayer(room, player, enemy, 13m));
        DamageResult third = Assert.Single(await DamagePlayer(room, player, enemy, 1m));

        Assert.Equal(12, first.UnblockedDamage);
        Assert.Equal(8, second.UnblockedDamage);
        Assert.Equal(0, third.UnblockedDamage);

        await Hook.BeforeSideTurnStart(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        DamageResult nextTurn = Assert.Single(await DamagePlayer(room, player, enemy, 7m));

        Assert.Equal(7, nextTurn.UnblockedDamage);
    }

    [Fact]
    public async Task BeatingRemnant_DoesNotCapFragrantMushroomDamageAfterCombat()
    {
        var runState = new RunState("beating-remnant-fragrant-mushroom", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        await Obtain<BeatingRemnant>(player);

        CombatRoom combatRoom = CreateCombatRoom();
        runState.PushRoom(combatRoom);
        await combatRoom.Enter(runState);
        Creature enemy = Assert.Single(combatRoom.Engine.State.Enemies);
        DamageResult combatHit = Assert.Single(await DamagePlayer(combatRoom, player, enemy, 15m));
        Assert.Equal(15, combatHit.UnblockedDamage);

        await CreatureCmd.Kill(enemy);
        Assert.True(combatRoom.Engine.CheckWinCondition());
        await combatRoom.ResolveOutcomeAsync(generateRewards: false);
        await combatRoom.Exit(runState);
        runState.PopCurrentRoom();
        int hpAfterCombat = player.Creature.CurrentHp;

        var eventRoom = new EventRoom(() =>
            (HungryForMushrooms)ModelDb.Event<HungryForMushrooms>().MutableClone());
        runState.PushRoom(eventRoom);
        await eventRoom.Enter(runState);
        await eventRoom.Event.ChooseOption(eventRoom.Event.CurrentOptions.Single(option =>
            option.Key == "FRAGRANT_MUSHROOM"));

        Assert.Equal(hpAfterCombat - 15, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Bellows_UpgradesEveryCardInTheOpeningHand()
    {
        (RunState runState, Player player) = CreateRun("bellows");
        await Obtain<Bellows>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        Assert.NotEmpty(player.PlayerCombatState!.Hand.Cards);
        Assert.All(player.PlayerCombatState.Hand.Cards, card => Assert.True(card.IsUpgraded));
    }

    [Fact]
    public async Task CaptainsWheel_GrantsEighteenBlockExactlyAtThirdTurnClear()
    {
        (RunState runState, Player player) = CreateRun("captains-wheel");
        await Obtain<CaptainsWheel>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);

        await room.Engine.EndPlayerTurnAsync();
        Assert.Equal(0, player.Creature.Block);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(18, player.Creature.Block);
    }

    [Fact]
    public async Task Chandelier_GrantsThreeEnergyOnThirdTurnOnly()
    {
        (RunState runState, Player player) = CreateRun("chandelier");
        await Obtain<Chandelier>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);

        Assert.Equal(3, player.PlayerCombatState!.Energy);
        await room.Engine.EndPlayerTurnAsync();
        Assert.Equal(3, player.PlayerCombatState.Energy);
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(6, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task CloakClasp_GrantsOneUnpoweredBlockPerCardHeldAtTurnEnd()
    {
        (RunState runState, Player player) = CreateRun("cloak-clasp");
        await Obtain<CloakClasp>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        int cardsHeld = player.PlayerCombatState!.Hand.Cards.Count;

        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);

        Assert.Equal(cardsHeld, player.Creature.Block);
    }

    [Fact]
    public async Task GamblingChip_ExplicitZeroSelectionLeavesAllOpeningPilesUnchanged()
    {
        (RunState controlRun, Player controlPlayer) = CreateRun("gambling-chip");
        CombatRoom controlRoom = CreateCombatRoom();
        await controlRoom.Enter(controlRun);

        (RunState relicRun, Player relicPlayer) = CreateRun("gambling-chip");
        await Obtain<GamblingChip>(relicPlayer);
        CombatRoom relicRoom = CreateCombatRoom();
        var selection = new LegacySelectionDecisionSource(chooseZero: true);
        relicRoom.ConfigureCardSelectionSource(selection);
        await relicRoom.Enter(relicRun);
        CardSelectionRequest request = Assert.Single(selection.Requests);
        Assert.Equal(0, request.MinCount); Assert.Equal(relicPlayer.PlayerCombatState!.Hand.Cards.Count, request.MaxCount); Assert.False(request.Cancelable);

        Assert.Equal(
            controlPlayer.PlayerCombatState!.Hand.Cards.Select(card => card.GetType()),
            relicPlayer.PlayerCombatState!.Hand.Cards.Select(card => card.GetType()));
        Assert.Equal(
            controlPlayer.PlayerCombatState.DrawPile.Cards.Select(card => card.GetType()),
            relicPlayer.PlayerCombatState.DrawPile.Cards.Select(card => card.GetType()));
        Assert.Empty(relicPlayer.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task GamePiece_DrawsOneAfterOwnedPowerCardFinishes()
    {
        (RunState runState, Player player) = CreateRun("game-piece");
        await Obtain<GamePiece>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        int drawBefore = player.PlayerCombatState!.DrawPile.Cards.Count;

        await room.Engine.PlayCardAsync(
            player,
            AddToHand<RareBatchPowerCard>(player),
            player.Creature);

        Assert.Equal(drawBefore - 1, player.PlayerCombatState.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task IceCream_CarriesUnusedEnergyStartingWithSecondTurn()
    {
        (RunState runState, Player player) = CreateRun("ice-cream");
        await Obtain<IceCream>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        player.PlayerCombatState!.LoseEnergy(2m);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(4, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task IntimidatingHelmet_GrantsFourBlockBeforeTwoEnergyCardResolves()
    {
        (RunState runState, Player player) = CreateRun("intimidating-helmet");
        await Obtain<IntimidatingHelmet>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);

        await room.Engine.PlayCardAsync(
            player,
            AddToHand<RareBatchSkillCard>(player),
            player.Creature);
        Assert.Equal(0, player.Creature.Block);

        await room.Engine.PlayCardAsync(
            player,
            AddToHand<RareBatchCostlySkillCard>(player),
            player.Creature);

        Assert.Equal(4, player.Creature.Block);
    }

    [Fact]
    public async Task Kunai_GrantsOneDexterityForEveryThirdOwnedAttackThisTurn()
    {
        (RunState runState, Player player) = CreateRun("kunai");
        await Obtain<Kunai>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        await PlayCopies<RareBatchAttackCard>(room, player, enemy, 2);
        Assert.Empty(player.Creature.Powers.OfType<DexterityPower>());
        await PlayCopies<RareBatchAttackCard>(room, player, enemy, 1);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);
        await PlayCopies<RareBatchAttackCard>(room, player, enemy, 3);

        Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);
    }

    [Fact]
    public async Task LizardTail_SeesLethalDamageThenPreventsOnlyFirstOwnedDeath()
    {
        (RunState runState, Player player) = CreateRun("lizard-tail");
        await Obtain<RareBatchDeathProbeRelic>(player);
        await Obtain<RareBatchNonPreventerRelic>(player);
        await Obtain<LizardTail>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];
        int expectedHeal = Math.Max(1, player.Creature.MaxHp / 2);

        DamageResult prevented = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));

        RareBatchDeathProbeRelic deathProbe =
            Assert.Single(player.Relics.OfType<RareBatchDeathProbeRelic>());
        RareBatchNonPreventerRelic nonPreventer =
            Assert.Single(player.Relics.OfType<RareBatchNonPreventerRelic>());
        LizardTail tail = Assert.Single(player.Relics.OfType<LizardTail>());
        Assert.Empty(deathProbe.Events);
        Assert.Equal(0, nonPreventer.PreventionCallbacks);
        Assert.True(prevented.WasTargetKilled);
        Assert.Equal(expectedHeal, player.Creature.CurrentHp);
        Assert.True(tail.IsUsedUp);

        DamageResult fatal = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));

        Assert.True(fatal.WasTargetKilled);
        Assert.Equal(new[] { "death" }, deathProbe.Events);
        Assert.Equal(0, nonPreventer.PreventionCallbacks);
    }

    [Fact]
    public async Task LizardTail_RemainsUsedAcrossCombatBoundaries()
    {
        (RunState runState, Player player) = CreateRun("lizard-tail-cross-combat");
        await Obtain<LizardTail>(player);
        CombatRoom firstRoom = CreateCombatRoom();
        await firstRoom.Enter(runState);
        Creature firstEnemy = firstRoom.Engine.State.Enemies[0];

        DamageResult prevented = Assert.Single(
            await DamagePlayer(firstRoom, player, firstEnemy, player.Creature.MaxHp * 2m));

        LizardTail tail = Assert.Single(player.Relics.OfType<LizardTail>());
        Assert.True(prevented.WasTargetKilled);
        Assert.True(tail.IsUsedUp);

        await firstRoom.Exit(runState);
        CombatRoom secondRoom = CreateCombatRoom();
        await secondRoom.Enter(runState);
        Creature secondEnemy = secondRoom.Engine.State.Enemies[0];

        DamageResult fatal = Assert.Single(
            await DamagePlayer(secondRoom, player, secondEnemy, player.Creature.MaxHp * 2m));

        Assert.True(fatal.WasTargetKilled);
        Assert.True(player.Creature.IsDead);
        Assert.True(tail.IsUsedUp);
    }

    [Fact]
    public async Task LizardTail_DoesNotPreventAnotherCreatureDeath()
    {
        (RunState runState, Player player) = CreateRun("lizard-tail-other");
        await Obtain<LizardTail>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        DamageResult result = Assert.Single(
            await CreatureCmd.Damage(
                room.Engine.State,
                new[] { enemy },
                enemy.MaxHp * 2m,
                ValueProp.Unblockable | ValueProp.Unpowered,
                player.Creature,
                null,
                null));

        Assert.True(result.WasTargetKilled);
        Assert.True(enemy.IsDead);
        Assert.False(Assert.Single(player.Relics.OfType<LizardTail>()).IsUsedUp);
    }

    [Fact]
    public async Task LizardTail_AlsoPreventsLethalJungleMazeSoloQuestDamage()
    {
        (RunState runState, Player player) = CreateRun("lizard-tail-event");
        await Obtain<LizardTail>(player);
        player.Creature.LoseHpInternal(player.Creature.CurrentHp - 10m, ValueProp.Unpowered);
        var adventure = (JungleMazeAdventure)ModelDb.Event<JungleMazeAdventure>().MutableClone();
        adventure.AssignOwner(player);
        adventure.BeginEvent(runState);

        await adventure.ChooseOption(
            adventure.CurrentOptions.Single(option => option.Key == "SOLO_QUEST"));

        LizardTail tail = Assert.Single(player.Relics.OfType<LizardTail>());
        Assert.Equal(Math.Max(1, player.Creature.MaxHp / 2), player.Creature.CurrentHp);
        Assert.True(tail.IsUsedUp);
    }

    private static Task Obtain<TRelic>(Player player)
        where TRelic : RelicModel =>
        RelicCmd.Obtain(ModelDb.Relic<TRelic>(), player);

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task PlayCopies<TCard>(
        CombatRoom room,
        Player player,
        Creature target,
        int count)
        where TCard : CardModel
    {
        for (int index = 0; index < count; index++)
        {
            await room.Engine.PlayCardAsync(player, AddToHand<TCard>(player), target);
        }
    }

    private static Task<IReadOnlyList<DamageResult>> DamagePlayer(
        CombatRoom room,
        Player player,
        Creature dealer,
        decimal amount) =>
        CreatureCmd.Damage(
            room.Engine.State,
            new[] { player.Creature },
            amount,
            ValueProp.Unblockable | ValueProp.Unpowered,
            dealer,
            null,
            null);

    private static CombatRoom CreateCombatRoom() =>
        new(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            RoomType.Monster);

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
