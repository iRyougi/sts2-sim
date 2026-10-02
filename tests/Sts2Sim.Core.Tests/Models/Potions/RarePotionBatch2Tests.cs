using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Potions;

file sealed class Task11AttackCard : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
}

file sealed class Task11SkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
}

file sealed class Task11AutomaticSpyPotion : PotionModel
{
    public bool WasUsed { get; private set; }

    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.Automatic;
    public override TargetType TargetType => TargetType.Self;

    protected override Task OnUse(Creature? target)
    {
        WasUsed = true;
        return Task.CompletedTask;
    }
}

[Collection("ModelDb")]
public sealed class RarePotionBatch2Tests : IDisposable
{
    public RarePotionBatch2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes.Concat(new[]
            {
                typeof(RarePotionBatch2TestCharacter),
                typeof(Task11AttackCard),
                typeof(Task11SkillCard),
                typeof(Task11AutomaticSpyPotion),
            }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<string, PotionUsage, TargetType, bool> MetadataCases => new()
    {
        { "FairyInABottle", PotionUsage.Automatic, TargetType.Self, false },
        { "FruitJuice", PotionUsage.AnyTime, TargetType.AnyPlayer, false },
        { "GigantificationPotion", PotionUsage.CombatOnly, TargetType.AnyPlayer, true },
        { "LiquidMemories", PotionUsage.CombatOnly, TargetType.AnyPlayer, true },
        { "LuckyTonic", PotionUsage.CombatOnly, TargetType.AnyPlayer, true },
        { "MazalethsGift", PotionUsage.CombatOnly, TargetType.AnyPlayer, true },
    };

    [Theory]
    [MemberData(nameof(MetadataCases))]
    public void Metadata_IsExactlyRareWithSpecifiedUsageTargetAndGenerationFlag(
        string potionName,
        PotionUsage usage,
        TargetType targetType,
        bool canBeGeneratedInCombat)
    {
        PotionModel potion = GetCanonicalPotion(potionName);
        PropertyInfo? generationProperty =
            typeof(PotionModel).GetProperty("CanBeGeneratedInCombat");

        Assert.Equal(PotionRarity.Rare, potion.Rarity);
        Assert.Equal(usage, potion.Usage);
        Assert.Equal(targetType, potion.TargetType);
        Assert.NotNull(generationProperty);
        Assert.Equal(canBeGeneratedInCombat, generationProperty!.GetValue(potion));
    }

    [Fact]
    public async Task PotionCmd_RejectsAutomaticPotionWithoutUsingOrConsumingIt()
    {
        (_, Player player) = CreateRun("automatic-manual-guard");
        var potion = Assert.IsType<Task11AutomaticSpyPotion>(
            player.AddPotionInternal(ModelDb.Potion<Task11AutomaticSpyPotion>()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PotionCmd.Use(potion, player, player.Creature));

        Assert.Contains(potion, player.PotionSlots);
        Assert.False(potion.WasUsed);
    }

    [Fact]
    public async Task FairyInABottle_PreventsActualLethalCombatDamageConsumesSlotAndOnlyWorksOnce()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("fairy-combat");
        PotionModel fairy = AddPotion("FairyInABottle", player);
        Creature enemy = room.Engine.State.Enemies.Single();
        int expectedHeal = Math.Max(1, (int)(player.Creature.MaxHp * 0.30m));

        DamageResult prevented = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));

        Assert.True(prevented.WasTargetKilled);
        Assert.Equal(expectedHeal, player.Creature.CurrentHp);
        Assert.DoesNotContain(fairy, player.PotionSlots);

        DamageResult fatal = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));

        Assert.True(fatal.WasTargetKilled);
        Assert.True(player.Creature.IsDead);
    }

    [Fact]
    public async Task FairyInABottle_AutomaticUseFiresNormalPotionLifecycleHooks()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("fairy-potion-hooks");
        await RelicCmd.Obtain(ModelDb.Relic<ReptileTrinket>(), player);
        AddPotion("FairyInABottle", player);
        Creature enemy = room.Engine.State.Enemies.Single();

        DamageResult prevented = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));

        Assert.True(prevented.WasTargetKilled);
        StrengthPower strength = Assert.Single(player.Creature.Powers.OfType<StrengthPower>());
        Assert.Equal(3, strength.Amount);
    }

    [Fact]
    public async Task FairyInABottle_PreventsRunLevelLethalHpLossAndHealsAtLeastOne()
    {
        (RunState runState, Player player) = CreateRun("fairy-run-level");
        player.Creature.SetMaxHpInternal(1m);
        PotionModel fairy = AddPotion("FairyInABottle", player);

        DamageResult result = await CreatureCmd.LoseHp(
            runState,
            player.Creature,
            99m,
            ValueProp.Unpowered);

        Assert.True(result.WasTargetKilled);
        Assert.Equal(1, player.Creature.CurrentHp);
        Assert.DoesNotContain(fairy, player.PotionSlots);
    }

    [Fact]
    public async Task FairyInABottle_PrecedesLizardTailThenLeavesThirdDeathFatal()
    {
        (RunState runState, Player player) = CreateRun("fairy-lizard-tail");
        await RelicCmd.Obtain(ModelDb.Relic<LizardTail>(), player);
        PotionModel fairy = AddPotion("FairyInABottle", player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies.Single();

        DamageResult first = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));

        Assert.True(first.WasTargetKilled);
        Assert.False(Assert.Single(player.Relics.OfType<LizardTail>()).IsUsedUp);
        Assert.DoesNotContain(fairy, player.PotionSlots);
        Assert.Equal(22, player.Creature.CurrentHp);

        DamageResult second = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));

        Assert.True(second.WasTargetKilled);
        Assert.DoesNotContain(fairy, player.PotionSlots);
        Assert.True(Assert.Single(player.Relics.OfType<LizardTail>()).IsUsedUp);
        Assert.Equal(37, player.Creature.CurrentHp);

        DamageResult third = Assert.Single(
            await DamagePlayer(room, player, enemy, player.Creature.MaxHp * 2m));
        Assert.True(third.WasTargetKilled);
    }

    [Fact]
    public async Task HeldPotion_IsRunListenerExactlyOnceInsideAndOutsideCombat()
    {
        (RunState runState, Player player) = CreateRun("held-potion-listener");
        PotionModel fairy = AddPotion("FairyInABottle", player);

        Assert.Equal(
            1,
            runState.IterateHookListeners(null)
                .Count(listener => ReferenceEquals(listener, fairy)));

        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);

        Assert.Equal(
            1,
            runState.IterateHookListeners(room.Engine.State)
                .Count(listener => ReferenceEquals(listener, fairy)));
    }

    [Fact]
    public async Task FruitJuice_OutOfCombatRaisesTargetsMaxAndCurrentHpByExactlyFive()
    {
        var runState = new RunState("fruit-juice-ally", new Overgrowth());
        Player owner = CreatePlayer(runState);
        Player ally = CreatePlayer(runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(ally);
        ally.Creature.LoseHpInternal(20m, ValueProp.Unpowered);
        int maxBefore = ally.Creature.MaxHp;
        int currentBefore = ally.Creature.CurrentHp;
        PotionModel juice = AddPotion("FruitJuice", owner);

        await PotionCmd.Use(juice, owner, ally.Creature);

        Assert.Equal(maxBefore + 5, ally.Creature.MaxHp);
        Assert.Equal(currentBefore + 5, ally.Creature.CurrentHp);
        Assert.DoesNotContain(juice, owner.PotionSlots);
    }

    [Fact]
    public async Task FruitJuice_RejectsForeignRunTargetWithoutConsumption()
    {
        (_, Player owner) = CreateRun("fruit-juice-owner");
        (_, Player foreign) = CreateRun("fruit-juice-foreign");
        PotionModel juice = AddPotion("FruitJuice", owner);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => PotionCmd.Use(juice, owner, foreign.Creature));

        Assert.Contains(juice, owner.PotionSlots);
    }

    [Fact]
    public async Task Gigantification_ClaimsExactCommandTriplesEveryHitConsumesOnceAndNotLater()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("gigantification-command");
        Creature enemy = room.Engine.State.Enemies.Single();
        await UsePotion("GigantificationPotion", player, player.Creature);
        PowerModel power = RequirePower(player.Creature, "GigantificationPower");
        Assert.Same(player.Creature, power.Applier);
        Assert.DoesNotContain(player.PotionSlots, potion => potion is not null);
        CardModel claimedSource = AddCard<Task11AttackCard>(player, PileType.Hand);
        CardModel unrelatedSource = AddCard<Task11AttackCard>(player, PileType.Hand);
        CardModel laterSource = AddCard<Task11AttackCard>(player, PileType.Hand);
        AttackCommand claimed = DamageCmd.Attack(2m)
            .WithHitCount(3)
            .FromCard(claimedSource, cardPlay: null)
            .Targeting(enemy);
        AttackCommand unrelated = DamageCmd.Attack(2m)
            .FromCard(unrelatedSource, cardPlay: null)
            .Targeting(enemy);
        AttackCommand later = DamageCmd.Attack(2m)
            .FromCard(laterSource, cardPlay: null)
            .Targeting(enemy);
        int hpBefore = enemy.CurrentHp;

        await Hook.BeforeAttack(room.Engine.State, claimed);
        await unrelated.Execute();

        Assert.Equal(hpBefore - 2, enemy.CurrentHp);
        Assert.Equal(1, power.Amount);
        Assert.Contains(power, player.Creature.Powers);

        await claimed.Execute();

        Assert.Equal(hpBefore - 20, enemy.CurrentHp);
        Assert.DoesNotContain(power, player.Creature.Powers);

        await later.Execute();
        Assert.Equal(hpBefore - 22, enemy.CurrentHp);

        (RunState sourceRun, Player donor) = CreateRun("gigantification-owner-source");
        Player recipient = CreatePlayer(sourceRun);
        sourceRun.AddPlayer(recipient);
        var sourceRoom = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await sourceRoom.Enter(sourceRun);
        await UsePotion("GigantificationPotion", donor, recipient.Creature);
        PowerModel recipientPower = RequirePower(recipient.Creature, "GigantificationPower");
        Assert.Equal(1, recipientPower.Amount);
        Assert.Same(donor.Creature, recipientPower.Applier);
        Assert.DoesNotContain(donor.PotionSlots, potion => potion is not null);
    }

    [Fact]
    public async Task Gigantification_IgnoresOtherOwnerSkillSourceAndUnpoweredAttack()
    {
        (RunState runState, Player owner) = CreateRun("gigantification-gates");
        Player ally = CreatePlayer(runState);
        runState.AddPlayer(ally);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies.Single();
        await UsePotion("GigantificationPotion", owner, owner.Creature);
        PowerModel power = RequirePower(owner.Creature, "GigantificationPower");
        CardModel allyAttack = AddCard<Task11AttackCard>(ally, PileType.Hand);
        CardModel ownerSkill = AddCard<Task11SkillCard>(owner, PileType.Hand);
        CardModel ownerUnpowered = AddCard<Task11AttackCard>(owner, PileType.Hand);
        CardModel ownerPowered = AddCard<Task11AttackCard>(owner, PileType.Hand);
        int hpBefore = enemy.CurrentHp;

        await DamageCmd.Attack(2m).FromCard(allyAttack, null).Targeting(enemy).Execute();
        await DamageCmd.Attack(2m).FromCard(ownerSkill, null).Targeting(enemy).Execute();
        await DamageCmd.Attack(2m).FromCard(ownerUnpowered, null).Unpowered().Targeting(enemy).Execute();

        Assert.Equal(hpBefore - 6, enemy.CurrentHp);
        Assert.Equal(1, power.Amount);

        await DamageCmd.Attack(2m).FromCard(ownerPowered, null).Targeting(enemy).Execute();

        Assert.Equal(hpBefore - 12, enemy.CurrentHp);
        Assert.DoesNotContain(power, owner.Creature.Powers);
    }

    [Fact]
    public async Task LiquidMemories_TakesFirstDiscardCardMakesOnlyItFreeThisTurnAndEmptyNoOps()
    {
        (Player player, _) = await CreateCombatAsync("liquid-memories");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearCombatPiles(player);
        CardModel first = AddCard<Task11AttackCard>(player, PileType.Discard);
        CardModel second = AddCard<Task11AttackCard>(player, PileType.Discard);

        await UsePotion("LiquidMemories", player, player.Creature);

        Assert.Equal([first], player.PlayerCombatState!.Hand.Cards);
        Assert.Equal([second], player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(0, first.EnergyCost);
        Assert.Equal(2, second.EnergyCost);

        CardPileCmd.Add(first, PileType.Hand);
        CardPileCmd.Add(second, PileType.Hand);
        PotionModel emptyPotion = AddPotion("LiquidMemories", player);
        await PotionCmd.Use(emptyPotion, player, player.Creature);

        Assert.Equal(2, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Empty(player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task LiquidMemories_FullHandKeepsSelectedCardInDiscardAndMarksItFree()
    {
        (Player player, _) = await CreateCombatAsync("liquid-memories-full-hand");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearCombatPiles(player);
        for (int index = 0; index < CardPile.MaxCardsInHand; index++)
        {
            AddCard<Task11AttackCard>(player, PileType.Hand);
        }
        CardModel selected = AddCard<Task11AttackCard>(player, PileType.Discard);

        await UsePotion("LiquidMemories", player, player.Creature);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Contains(selected, player.PlayerCombatState.DiscardPile.Cards);
        Assert.True(selected.TemporaryFreeThisTurn);
        Assert.Equal(0, selected.EnergyCost);
    }

    [Fact]
    public async Task Buffer_DoesNotConsumeForBlockedZeroThenConsumesIndependentPositiveOwnerEvents()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("buffer-events");
        Creature enemy = room.Engine.State.Enemies.Single();
        await UsePotion("LuckyTonic", player, player.Creature);
        await UsePotion("LuckyTonic", player, player.Creature);
        PowerModel buffer = RequirePower(player.Creature, "BufferPower");
        Assert.Same(player.Creature, buffer.Applier);
        Assert.DoesNotContain(player.PotionSlots, potion => potion is not null);
        int hpBefore = player.Creature.CurrentHp;
        player.Creature.GainBlockInternal(5m);

        DamageResult blocked = Assert.Single(
            await DamagePlayer(room, player, enemy, 5m, ValueProp.Unpowered));

        Assert.Equal(0, blocked.UnblockedDamage);
        Assert.Equal(2, buffer.Amount);

        DamageResult firstPrevented = Assert.Single(
            await DamagePlayer(room, player, enemy, 4m, ValueProp.Unpowered));
        DamageResult secondPrevented = Assert.Single(
            await DamagePlayer(room, player, enemy, 4m, ValueProp.Unpowered));

        Assert.Equal(0, firstPrevented.UnblockedDamage);
        Assert.Equal(0, secondPrevented.UnblockedDamage);
        Assert.DoesNotContain(buffer, player.Creature.Powers);
        Assert.Equal(hpBefore, player.Creature.CurrentHp);

        DamageResult unbuffered = Assert.Single(
            await DamagePlayer(room, player, enemy, 3m, ValueProp.Unpowered));
        Assert.Equal(3, unbuffered.UnblockedDamage);
        Assert.Equal(hpBefore - 3, player.Creature.CurrentHp);

        (RunState sourceRun, Player donor) = CreateRun("buffer-owner-source");
        Player recipient = CreatePlayer(sourceRun);
        sourceRun.AddPlayer(recipient);
        var sourceRoom = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await sourceRoom.Enter(sourceRun);
        await UsePotion("LuckyTonic", donor, recipient.Creature);
        PowerModel recipientBuffer = RequirePower(recipient.Creature, "BufferPower");
        Assert.Equal(1, recipientBuffer.Amount);
        Assert.Same(donor.Creature, recipientBuffer.Applier);
        Assert.DoesNotContain(donor.PotionSlots, potion => potion is not null);
    }

    [Fact]
    public async Task Buffer_DoesNotConsumeForAnotherCreature()
    {
        (RunState runState, Player owner) = CreateRun("buffer-non-owner");
        Player ally = CreatePlayer(runState);
        runState.AddPlayer(ally);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies.Single();
        await UsePotion("LuckyTonic", owner, owner.Creature);
        PowerModel buffer = RequirePower(owner.Creature, "BufferPower");
        int allyHpBefore = ally.Creature.CurrentHp;

        DamageResult allyResult = Assert.Single(
            await DamagePlayer(room, ally, enemy, 3m, ValueProp.Unpowered));

        Assert.Equal(3, allyResult.UnblockedDamage);
        Assert.Equal(allyHpBefore - 3, ally.Creature.CurrentHp);
        Assert.Equal(1, buffer.Amount);
        Assert.Contains(buffer, owner.Creature.Powers);
    }

    [Fact]
    public async Task Ritual_PlayerGainsStrengthImmediatelyAtEachEligibleEndWithoutDecay()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("ritual-player");
        await UsePotion("MazalethsGift", player, player.Creature);
        PowerModel ritual = RequirePower(player.Creature, "RitualPower");
        Assert.Same(player.Creature, ritual.Applier);
        Assert.DoesNotContain(player.PotionSlots, potion => potion is not null);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);
        Assert.Empty(player.Creature.Powers.OfType<StrengthPower>());

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
        Assert.Equal(1, ritual.Amount);
        Assert.Contains(ritual, player.Creature.Powers);

        (RunState sourceRun, Player donor) = CreateRun("ritual-owner-source");
        Player recipient = CreatePlayer(sourceRun);
        sourceRun.AddPlayer(recipient);
        var sourceRoom = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await sourceRoom.Enter(sourceRun);
        await UsePotion("MazalethsGift", donor, recipient.Creature);
        PowerModel recipientRitual = RequirePower(recipient.Creature, "RitualPower");
        Assert.Equal(1, recipientRitual.Amount);
        Assert.Same(donor.Creature, recipientRitual.Applier);
        Assert.DoesNotContain(donor.PotionSlots, potion => potion is not null);
    }

    [Fact]
    public async Task Ritual_EnemySkipsExactlyFirstEligibleEndThenGainsRepeatedStrength()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("ritual-enemy");
        Creature enemy = room.Engine.State.Enemies.Single();
        PowerModel ritual = (await PowerCmd.Apply(
            room.Engine.State,
            RequireTask11Type("RitualPower"),
            enemy,
            1m,
            player.Creature,
            cardSource: null))!;

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);
        Assert.Empty(enemy.Powers.OfType<StrengthPower>());

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);
        Assert.Equal(1, Assert.Single(enemy.Powers.OfType<StrengthPower>()).Amount);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);
        Assert.Equal(2, Assert.Single(enemy.Powers.OfType<StrengthPower>()).Amount);
        Assert.Equal(1, ritual.Amount);
    }

    [Fact]
    public async Task Gigantification_StackedChargesTripleTwoQualifiedCommandsThenExpire()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("gigantification-stacks");
        Creature enemy = room.Engine.State.Enemies.Single();
        await UsePotion("GigantificationPotion", player, player.Creature);
        await UsePotion("GigantificationPotion", player, player.Creature);
        PowerModel power = RequirePower(player.Creature, "GigantificationPower");
        CardModel first = AddCard<Task11AttackCard>(player, PileType.Hand);
        CardModel second = AddCard<Task11AttackCard>(player, PileType.Hand);
        int hpBefore = enemy.CurrentHp;

        await DamageCmd.Attack(2m).FromCard(first, null).Targeting(enemy).Execute();

        Assert.Equal(hpBefore - 6, enemy.CurrentHp);
        Assert.Equal(1, power.Amount);
        Assert.Contains(power, player.Creature.Powers);

        await DamageCmd.Attack(2m).FromCard(second, null).Targeting(enemy).Execute();

        Assert.Equal(hpBefore - 12, enemy.CurrentHp);
        Assert.DoesNotContain(power, player.Creature.Powers);
    }

    [Fact]
    public async Task Ritual_StrengthUsesRitualOwnerAsApplier()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("ritual-applier");
        await UsePotion("MazalethsGift", player, player.Creature);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);

        StrengthPower strength = Assert.Single(player.Creature.Powers.OfType<StrengthPower>());
        Assert.Same(player.Creature, strength.Applier);
    }
    private static PotionModel GetCanonicalPotion(string potionName)
    {
        Type potionType = RequireTask11Type(potionName);
        return ModelDb.GetById<PotionModel>(ModelDb.GetId(potionType));
    }

    private static PotionModel AddPotion(string potionName, Player owner) =>
        owner.AddPotionInternal(GetCanonicalPotion(potionName));

    private static async Task UsePotion(
        string potionName,
        Player owner,
        Creature target)
    {
        PotionModel potion = AddPotion(potionName, owner);
        await PotionCmd.Use(potion, owner, target);
    }

    private static PowerModel RequirePower(Creature owner, string name) =>
        Assert.Single(owner.Powers, power => power.GetType() == RequireTask11Type(name));

    private static Type RequireTask11Type(string name)
    {
        string category = name.EndsWith("Power", StringComparison.Ordinal)
            ? "Powers"
            : "Potions";
        return typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.{category}.{name}")
            ?? throw new Xunit.Sdk.XunitException($"Task 11 model {name} is not implemented.");
    }

    private static TCard AddCard<TCard>(Player player, PileType pile)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToList())
            {
                CardPileCmd.Remove(card);
            }
        }
    }

    private static Task<IReadOnlyList<DamageResult>> DamagePlayer(
        CombatRoom room,
        Player player,
        Creature dealer,
        decimal amount,
        ValueProp props = ValueProp.Unblockable | ValueProp.Unpowered) =>
        CreatureCmd.Damage(
            room.Engine.State,
            new[] { player.Creature },
            amount,
            props,
            dealer,
            cardSource: null,
            cardPlay: null);

    private static (RunState runState, Player player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = CreatePlayer(runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static Player CreatePlayer(RunState runState) =>
        Player.CreateForNewRun(
            ModelDb.Character<RarePotionBatch2TestCharacter>(),
            runState);

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(
        string seed)
    {
        (RunState runState, Player player) = CreateRun(seed);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

file sealed class RarePotionBatch2TestCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;
}
