using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

file sealed class Task9AttackCard : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task9SkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task9EtherealSkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Ethereal };
}

file sealed class Task9PowerCard : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task9MoveRemainingCardSkill : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        CardModel? selected = Owner.PlayerCombatState!.Hand.Cards.FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        }

        return Task.CompletedTask;
    }
}

file sealed class Task9DoubleDebuffCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<WeakPower>(
            CombatState!,
            cardPlay.Target,
            2m,
            Owner.Creature,
            this);
        await PowerCmd.Apply<VulnerablePower>(
            CombatState!,
            cardPlay.Target,
            3m,
            Owner.Creature,
            this);
    }
}

file sealed class Task9CalendarMonster : MonsterModel
{
    public override int MinInitialHp => 100;
    public override int MaxInitialHp => 100;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState(
            "WAIT",
            _ => Task.CompletedTask,
            new SingleAttackIntent(0));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}

file sealed class Task9CourierDecisionSource : IRunDecisionSource
{
    private int? _targetPurchases;

    public int InitialMaximumPurchases { get; private set; }
    public int BuyCount { get; private set; }

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(point => point.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<ShopDecision> ChooseShopActionAsync(MerchantInventory inventory, Player player)
    {
        if (_targetPurchases is null)
        {
            InitialMaximumPurchases =
                inventory.Cards.Count + inventory.Relics.Count + inventory.Potions.Count + 1;
            _targetPurchases = InitialMaximumPurchases + 1;
        }

        if (BuyCount >= _targetPurchases.Value)
        {
            return Task.FromResult<ShopDecision>(new ShopDecision.Leave());
        }

        BuyCount++;
        return Task.FromResult<ShopDecision>(new ShopDecision.BuyCard(inventory.Cards[0]));
    }
}

[Collection("ModelDb")]
public sealed class RareRelicBatch3Tests : IDisposable
{
    public RareRelicBatch3Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(Task9AttackCard))
                .Append(typeof(Task9SkillCard))
                .Append(typeof(Task9EtherealSkillCard))
                .Append(typeof(Task9PowerCard))
                .Append(typeof(Task9MoveRemainingCardSkill))
                .Append(typeof(Task9DoubleDebuffCard))
                .Append(typeof(Task9CalendarMonster)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Task9Relics_AreExactlyTheElevenRegisteredRareModels()
    {
        RelicModel[] relics =
        {
            ModelDb.Relic<RainbowRing>(),
            ModelDb.Relic<RazorTooth>(),
            ModelDb.Relic<Shuriken>(),
            ModelDb.Relic<StoneCalendar>(),
            ModelDb.Relic<SturdyClamp>(),
            ModelDb.Relic<TheCourier>(),
            ModelDb.Relic<TungstenRod>(),
            ModelDb.Relic<UnceasingTop>(),
            ModelDb.Relic<UnsettlingLamp>(),
            ModelDb.Relic<VexingPuzzlebox>(),
            ModelDb.Relic<WhiteBeastStatue>(),
        };

        Assert.Equal(11, relics.Length);
        Assert.All(relics, relic => Assert.Equal(RelicRarity.Rare, relic.Rarity));
        Assert.False(ModelDb.Relic<TheCourier>().IsAllowedInShops);
    }

    [Fact]
    public async Task RainbowRing_RequiresAllThreeCardTypesAndTriggersOncePerTurn()
    {
        (RunState runState, Player player) = CreateRun("rainbow-ring");
        await Obtain<RainbowRing>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        Creature enemy = room.Engine.State.Enemies[0];

        await PlayThreeTypes(room, player, enemy);

        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);

        await PlayThreeTypes(room, player, enemy);

        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);

        await Hook.BeforeSideTurnStart(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        await PlayThreeTypes(room, player, enemy);

        Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
        Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);
    }

    [Fact]
    public async Task RazorTooth_UpgradesOwnedAttackAndSkillButNotPower()
    {
        (RunState runState, Player player) = CreateRun("razor-tooth");
        await Obtain<RazorTooth>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];
        Task9AttackCard attack = AddToHand<Task9AttackCard>(player);
        Task9SkillCard skill = AddToHand<Task9SkillCard>(player);
        Task9PowerCard power = AddToHand<Task9PowerCard>(player);

        await room.Engine.PlayCardAsync(player, attack, enemy);
        await room.Engine.PlayCardAsync(player, skill, null);
        await room.Engine.PlayCardAsync(player, power, player.Creature);

        Assert.True(attack.IsUpgraded);
        Assert.True(skill.IsUpgraded);
        Assert.False(power.IsUpgraded);
    }

    [Fact]
    public async Task Shuriken_GrantsStrengthForEveryThirdOwnedAttack()
    {
        (RunState runState, Player player) = CreateRun("shuriken");
        await Obtain<Shuriken>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        await PlayCopies<Task9AttackCard>(room, player, enemy, 2);
        Assert.Empty(player.Creature.Powers.OfType<StrengthPower>());
        await PlayCopies<Task9AttackCard>(room, player, enemy, 1);
        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
        await PlayCopies<Task9AttackCard>(room, player, enemy, 3);

        Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task StoneCalendar_DamagesEveryHittableEnemyForFiftyTwoOnlyOnTurnSeven()
    {
        (RunState runState, Player player) = CreateRun("stone-calendar");
        await Obtain<StoneCalendar>(player);
        CombatRoom room = CreateCalendarCombatRoom();
        await room.Enter(runState);
        Creature secondEnemy = room.Engine.State.AddMonster(
            (MonsterModel)ModelDb.Monster<Task9CalendarMonster>().MutableClone(),
            CombatSide.Enemy);

        player.PlayerCombatState!.TurnNumber = 6;
        await Hook.AfterSideTurnStart(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        Assert.All(room.Engine.State.Enemies, enemy => Assert.Equal(100, enemy.CurrentHp));

        player.PlayerCombatState.TurnNumber = 7;
        await Hook.AfterSideTurnStart(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);

        Assert.Equal(48, room.Engine.State.Enemies[0].CurrentHp);
        Assert.Equal(48, secondEnemy.CurrentHp);

        player.PlayerCombatState.TurnNumber = 14;
        await Hook.AfterSideTurnStart(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);

        Assert.Equal(48, room.Engine.State.Enemies[0].CurrentHp);
        Assert.Equal(48, secondEnemy.CurrentHp);
    }

    [Fact]
    public async Task SturdyClamp_PreservesOwnedBlockAtOrBelowTen()
    {
        (RunState runState, Player player) = CreateRun("sturdy-clamp-low");
        await Obtain<SturdyClamp>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        player.Creature.GainBlockInternal(7m);

        bool shouldClear = Hook.ShouldClearBlock(room.Engine.State, player.Creature);

        Assert.False(shouldClear);
        Assert.Equal(7, player.Creature.Block);
    }

    [Fact]
    public async Task SturdyClamp_ReducesOwnedBlockAboveTenToExactlyTen()
    {
        (RunState runState, Player player) = CreateRun("sturdy-clamp-high");
        await Obtain<SturdyClamp>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        player.Creature.GainBlockInternal(18m);

        bool shouldClear = Hook.ShouldClearBlock(room.Engine.State, player.Creature, out AbstractModel? preventer);

        Assert.False(shouldClear);
        Assert.Equal(18, player.Creature.Block);
        await Hook.AfterPreventingBlockClear(room.Engine.State, preventer!, player.Creature);
        Assert.Equal(10, player.Creature.Block);
    }

    [Fact]
    public async Task TheCourier_DiscountsEveryMerchantCategoryByTwentyPercent()
    {
        (RunState controlRun, Player controlPlayer) = CreateRun("courier-prices");
        MerchantRoom controlRoom = await EnterMerchant(controlRun);
        MerchantInventory control = controlRoom.Inventory;

        (RunState courierRun, Player courierPlayer) = CreateRun("courier-prices");
        await Obtain<TheCourier>(courierPlayer);
        MerchantRoom courierRoom = await EnterMerchant(courierRun);
        MerchantInventory discounted = courierRoom.Inventory;

        Assert.Equal(control.Cards.Count, discounted.Cards.Count);
        Assert.Equal(control.Relics.Count, discounted.Relics.Count);
        Assert.Equal(control.Potions.Count, discounted.Potions.Count);
        AssertDiscounted(
            control.Cards.Select(entry => entry.Price),
            discounted.Cards.Select(entry => entry.Price));
        AssertDiscounted(
            control.Relics.Select(entry => entry.Price),
            discounted.Relics.Select(entry => entry.Price));
        AssertDiscounted(
            control.Potions.Select(entry => entry.Price),
            discounted.Potions.Select(entry => entry.Price));
        Assert.Equal((int)(control.CardRemoval.Price * 0.8m), discounted.CardRemoval.Price);
    }

    [Fact]
    public async Task TheCourier_RefillsPurchasedCardWithFreshEntryThatCanBeBoughtAgain()
    {
        (RunState runState, Player player) = CreateRun("courier-refill-room");
        player.Gold = 999999;
        await Obtain<TheCourier>(player);
        MerchantRoom room = await EnterMerchant(runState);
        int deckBefore = player.Deck.Cards.Count;
        MerchantCardEntry first = room.Inventory.Cards[0];

        await room.Buy(first, player);

        MerchantCardEntry second = room.Inventory.Cards[0];
        Assert.True(first.Purchased);
        Assert.NotSame(first, second);
        Assert.False(second.Purchased);

        await room.Buy(second, player);

        Assert.True(second.Purchased);
        Assert.NotSame(second, room.Inventory.Cards[0]);
        Assert.Equal(deckBefore + 2, player.Deck.Cards.Count);
    }

    [Fact]
    public async Task TheCourier_RefillsPotionAndRelicWithFreshUnownedPurchasableEntries()
    {
        (RunState runState, Player player) = CreateRun("courier-refill-owned-models");
        player.Gold = 999999;
        await Obtain<TheCourier>(player);
        MerchantRoom room = await EnterMerchant(runState);
        MerchantPotionEntry firstPotion = room.Inventory.Potions[0];
        MerchantRelicEntry firstRelic = room.Inventory.Relics[0];

        await room.Buy(firstPotion, player);

        MerchantPotionEntry secondPotion = room.Inventory.Potions[0];
        Assert.True(firstPotion.Purchased);
        Assert.Same(player, firstPotion.Potion.Owner);
        Assert.NotSame(firstPotion, secondPotion);
        Assert.False(secondPotion.Purchased);
        Assert.Null(secondPotion.Potion.Owner);
        await room.Buy(secondPotion, player);
        Assert.True(secondPotion.Purchased);

        await room.Buy(firstRelic, player);

        MerchantRelicEntry secondRelic = room.Inventory.Relics[0];
        Assert.True(firstRelic.Purchased);
        Assert.Contains(player.Relics, relic => relic.Id == firstRelic.Relic.Id);
        Assert.NotSame(firstRelic, secondRelic);
        Assert.NotSame(firstRelic.Relic, secondRelic.Relic);
        Assert.False(secondRelic.Purchased);
        Assert.Null(secondRelic.Relic.Owner);
        await room.Buy(secondRelic, player);
        Assert.True(secondRelic.Purchased);
    }

    [Fact]
    public async Task TheCourier_RunDriverCanBuyPastOriginalShelfMaximumThenLeave()
    {
        (RunState runState, Player player) = CreateRun("courier-run-driver");
        player.Gold = 999999;
        await Obtain<TheCourier>(player);
        MakeFirstReachableRoomAShop(runState);
        int deckBefore = player.Deck.Cards.Count;
        var decisions = new Task9CourierDecisionSource();
        var driver = new RunDriver(runState, decisions);

        RunDriver.Result result = await driver.RunAsync(maxFloors: 1);

        Assert.Equal(1, result.FloorsVisited);
        Assert.True(decisions.BuyCount > decisions.InitialMaximumPurchases);
        Assert.Equal(deckBefore + decisions.BuyCount, player.Deck.Cards.Count);
    }

    [Fact]
    public async Task TungstenRod_ReducesEachOwnedHpLossByOneWithZeroFloor()
    {
        (RunState runState, Player player) = CreateRun("tungsten-rod");
        await Obtain<TungstenRod>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        DamageResult one = Assert.Single(await DamagePlayer(room, player, enemy, 1m));
        DamageResult ten = Assert.Single(await DamagePlayer(room, player, enemy, 10m));

        Assert.Equal(0, one.UnblockedDamage);
        Assert.Equal(9, ten.UnblockedDamage);
    }

    [Fact]
    public async Task TungstenRod_ComposesAfterBeatingRemnantCumulativeCap()
    {
        (RunState runState, Player player) = CreateRun("tungsten-remnant");
        await Obtain<BeatingRemnant>(player);
        await Obtain<TungstenRod>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        DamageResult first = Assert.Single(await DamagePlayer(room, player, enemy, 15m));
        DamageResult second = Assert.Single(await DamagePlayer(room, player, enemy, 15m));
        DamageResult third = Assert.Single(await DamagePlayer(room, player, enemy, 1m));

        Assert.Equal(14, first.UnblockedDamage);
        Assert.Equal(5, second.UnblockedDamage);
        Assert.Equal(0, third.UnblockedDamage);
    }

    [Fact]
    public async Task UnceasingTop_DrawsOneAfterLastPlayedCardFinishesResolving()
    {
        (RunState runState, Player player) = CreateRun("unceasing-play");
        await Obtain<UnceasingTop>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        int drawBefore = player.PlayerCombatState!.DrawPile.Cards.Count;

        await room.Engine.PlayCardAsync(
            player,
            AddToHand<Task9SkillCard>(player),
            null);

        Assert.Single(player.PlayerCombatState.Hand.Cards);
        Assert.Equal(drawBefore - 1, player.PlayerCombatState.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task UnceasingTop_WaitsForStormEffectAndChecksEmptyHandAfterPotion()
    {
        (RunState runState, Player player) = CreateRun("unceasing-effect-boundary");
        await Obtain<UnceasingTop>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        AddToHand<Task9SkillCard>(player);
        var gamble = (Sts2Sim.Core.Models.Cards.StormOfSteel)ModelDb
            .Card<Sts2Sim.Core.Models.Cards.StormOfSteel>().MutableClone();
        gamble.AssignOwner(player);
        CardPileCmd.Add(gamble, PileType.Hand);
        await room.Engine.PlayCardAsync(player, gamble, player.Creature);
        // One discarded card yields one Shiv; Top must not draw mid-effect.
        Assert.Single(player.PlayerCombatState!.Hand.Cards);

        ClearHand(player);
        var potion = player.AddPotionInternal(ModelDb.Potion<Sts2Sim.Core.Models.Potions.StrengthPotion>());
        await PotionCmd.Use(potion, player, player.Creature);
        Assert.Single(player.PlayerCombatState.Hand.Cards);
    }
    [Fact]
    public async Task UnceasingTop_DrawsOneAfterActiveExhaustEmptiesHand()
    {
        (RunState runState, Player player) = CreateRun("unceasing-exhaust");
        await Obtain<UnceasingTop>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        Task9SkillCard exhausted = AddToHand<Task9SkillCard>(player);
        int drawBefore = player.PlayerCombatState!.DrawPile.Cards.Count;

        await CardPileCmd.Exhaust(room.Engine.State, exhausted);

        Assert.Single(player.PlayerCombatState.Hand.Cards);
        Assert.Equal(drawBefore - 1, player.PlayerCombatState.DrawPile.Cards.Count);
        Assert.Contains(exhausted, player.PlayerCombatState.ExhaustPile.Cards);
    }

    [Fact]
    public async Task UnceasingTop_SeesEffectMoveTheRemainingHandCardBeforePlayCompletes()
    {
        (RunState runState, Player player) = CreateRun("unceasing-effect");
        await Obtain<UnceasingTop>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        Task9SkillCard moved = AddToHand<Task9SkillCard>(player);
        Task9MoveRemainingCardSkill effect = AddToHand<Task9MoveRemainingCardSkill>(player);

        await room.Engine.PlayCardAsync(player, effect, null);

        Assert.Same(moved, Assert.Single(player.PlayerCombatState!.Hand.Cards));
    }

    [Fact]
    public async Task UnceasingTop_DoesNotTriggerDuringNormalOrEtherealEndTurnFlush()
    {
        (RunState normalRun, Player normalPlayer) = CreateRun("unceasing-flush-normal");
        await Obtain<UnceasingTop>(normalPlayer);
        CombatRoom normalRoom = CreateCombatRoom();
        await normalRoom.Enter(normalRun);
        ClearHand(normalPlayer);
        AddToHand<Task9SkillCard>(normalPlayer);

        await normalRoom.Engine.EndPlayerTurnAsync();

        Assert.Equal(5, normalPlayer.PlayerCombatState!.Hand.Cards.Count);

        (RunState etherealRun, Player etherealPlayer) = CreateRun("unceasing-flush-ethereal");
        await Obtain<UnceasingTop>(etherealPlayer);
        CombatRoom etherealRoom = CreateCombatRoom();
        await etherealRoom.Enter(etherealRun);
        ClearHand(etherealPlayer);
        Task9EtherealSkillCard ethereal = AddToHand<Task9EtherealSkillCard>(etherealPlayer);
        int drawBefore = etherealPlayer.PlayerCombatState!.DrawPile.Cards.Count;

        await CardPileCmd.Exhaust(
            etherealRoom.Engine.State,
            ethereal,
            causedByEthereal: true);

        Assert.Empty(etherealPlayer.PlayerCombatState.Hand.Cards);
        Assert.Equal(drawBefore, etherealPlayer.PlayerCombatState.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task UnceasingTop_DoesNotAddAnExtraDrawAfterRestlessnessRefillsHand()
    {
        (RunState runState, Player player) = CreateRun("unceasing-restlessness");
        await Obtain<UnceasingTop>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        var restlessness =
            (Sts2Sim.Core.Models.Cards.Restlessness)ModelDb
                .Card<Sts2Sim.Core.Models.Cards.Restlessness>()
                .MutableClone();
        restlessness.AssignOwner(player);
        CardPileCmd.Add(restlessness, PileType.Hand);
        int energyBefore = player.PlayerCombatState!.Energy;

        await room.Engine.PlayCardAsync(player, restlessness, null);

        Assert.Equal(2, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(energyBefore + 2, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task UnceasingTop_DoesNotDoubleDrawWhenGamePieceAlreadyRefillsLastPower()
    {
        (RunState runState, Player player) = CreateRun("unceasing-game-piece");
        await Obtain<GamePiece>(player);
        await Obtain<UnceasingTop>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        int drawBefore = player.PlayerCombatState!.DrawPile.Cards.Count;

        await room.Engine.PlayCardAsync(
            player,
            AddToHand<Task9PowerCard>(player),
            player.Creature);

        Assert.Single(player.PlayerCombatState.Hand.Cards);
        Assert.Equal(drawBefore - 1, player.PlayerCombatState.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task UnsettlingLamp_DoublesEveryDebuffFromFirstQualifyingCardOnly()
    {
        (RunState runState, Player player) = CreateRun("unsettling-lamp");
        await Obtain<UnsettlingLamp>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        await PowerCmd.Apply<WeakPower>(
            room.Engine.State,
            enemy,
            1m,
            player.Creature,
            null);
        Assert.Equal(1, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount);

        await room.Engine.PlayCardAsync(
            player,
            AddToHand<Task9DoubleDebuffCard>(player),
            enemy);

        Assert.Equal(5, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount);
        Assert.Equal(6, Assert.Single(enemy.Powers.OfType<VulnerablePower>()).Amount);

        await room.Engine.PlayCardAsync(
            player,
            AddToHand<Task9DoubleDebuffCard>(player),
            enemy);

        Assert.Equal(7, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount);
        Assert.Equal(9, Assert.Single(enemy.Powers.OfType<VulnerablePower>()).Amount);
    }

    [Fact]
    public async Task VexingPuzzlebox_GeneratesOneOwnedNonColorlessFreeCardOnFirstTurnOnly()
    {
        (RunState runState, Player player) = CreateRun("vexing-puzzlebox");
        await Obtain<VexingPuzzlebox>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        CardModel generated =
            Assert.Single(
                player.PlayerCombatState!.Hand.Cards, card => card.TemporaryFreeThisTurn);
        Assert.False(generated.IsColorless);
        Assert.Same(player, generated.Owner);
        Assert.Equal(1, player.PlayerCombatState.CardsGeneratedThisCombat);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(1, player.PlayerCombatState.CardsGeneratedThisCombat);
    }

    [Fact]
    public async Task WhiteBeastStatue_ForcesPotionForCombatRewardsButNotOtherRooms()
    {
        (RunState runState, Player player) = CreateRun("white-beast-statue");
        await Obtain<WhiteBeastStatue>(player);

        RewardsSet rewards = RewardsSet.GenerateFor(player, RoomType.Monster, runState);

        Assert.NotNull(rewards.Potion);
        Assert.False(Hook.ShouldForcePotionReward(runState, RoomType.Shop));
    }

    private static async Task PlayThreeTypes(
        CombatRoom room,
        Player player,
        Creature enemy)
    {
        await room.Engine.PlayCardAsync(
            player,
            AddToHand<Task9AttackCard>(player),
            enemy);
        await room.Engine.PlayCardAsync(
            player,
            AddToHand<Task9SkillCard>(player),
            null);
        await room.Engine.PlayCardAsync(
            player,
            AddToHand<Task9PowerCard>(player),
            player.Creature);
    }

    private static void AssertDiscounted(
        IEnumerable<int> original,
        IEnumerable<int> discounted) =>
        Assert.Equal(original.Select(price => (int)(price * 0.8m)), discounted);

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

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
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
            await room.Engine.PlayCardAsync(
                player,
                AddToHand<TCard>(player),
                target);
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
        new(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

    private static CombatRoom CreateCalendarCombatRoom() =>
        new(() => (MonsterModel)ModelDb.Monster<Task9CalendarMonster>().MutableClone());

    private static async Task<MerchantRoom> EnterMerchant(RunState runState)
    {
        var room = new MerchantRoom();
        runState.PushRoom(room);
        await room.Enter(runState);
        return room;
    }

    private static void MakeFirstReachableRoomAShop(RunState runState) =>
        runState.Map.StartingMapPoint.Children
            .OrderBy(point => point.coord.col)
            .First()
            .PointType = MapPointType.Shop;

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
