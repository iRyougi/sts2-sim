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
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

file sealed class DoubleRestSiteHealRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyRestSiteHealAmount(
        Sts2Sim.Core.Entities.Creatures.Creature creature,
        decimal amount) =>
        creature == Owner.Creature ? amount * 2m : amount;
}

file sealed class AddFiveRestSiteHealRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyRestSiteHealAmount(
        Sts2Sim.Core.Entities.Creatures.Creature creature,
        decimal amount) =>
        creature == Owner.Creature ? amount + 5m : amount;
}

[Collection("ModelDb")]
public sealed class CommonRelicBatch2Tests : IDisposable
{
    public CommonRelicBatch2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(DoubleRestSiteHealRelic))
                .Append(typeof(AddFiveRestSiteHealRelic)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Lantern_GrantsOneEnergyOnlyOnOwnersFirstPlayerTurn()
    {
        (RunState runState, Player player) = CreateRun("lantern");
        await Obtain<Lantern>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        Assert.Equal(player.MaxEnergy + 1, player.PlayerCombatState!.Energy);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(player.MaxEnergy, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task MealTicket_HealsOnlyAfterEnteringMerchantRoom()
    {
        (RunState runState, Player player) = CreateRun("meal-ticket");
        await Obtain<MealTicket>(player);
        player.Creature.LoseHpInternal(30m, ValueProp.Unpowered);
        int woundedHp = player.Creature.CurrentHp;

        await new RestSiteRoom().Enter(runState);
        Assert.Equal(woundedHp, player.Creature.CurrentHp);

        await new MerchantRoom().Enter(runState);

        Assert.Equal(woundedHp + 15, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task MealTicket_DoesNotReviveDeadOwner()
    {
        (RunState runState, Player player) = CreateRun("meal-ticket-dead");
        await Obtain<MealTicket>(player);
        player.Creature.LoseHpInternal(player.Creature.CurrentHp, ValueProp.Unpowered);
        Assert.True(player.Creature.IsDead);

        await new MerchantRoom().Enter(runState);

        Assert.True(player.Creature.IsDead);
        Assert.Equal(0, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task OddlySmoothStone_AppliesOneDexterityOnCombatRoomEntry()
    {
        (RunState runState, Player player) = CreateRun("oddly-smooth-stone");
        await Obtain<OddlySmoothStone>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        DexterityPower dexterity = Assert.Single(player.Creature.Powers.OfType<DexterityPower>());
        Assert.Equal(1, dexterity.Amount);
    }

    [Fact]
    public async Task Pendulum_DrawsEveryThirdPlayerTurn_AcrossCombatBoundaries()
    {
        (RunState runState, Player player) = CreateRun("pendulum");
        await Obtain<Pendulum>(player);
        CombatRoom firstRoom = CreateCombatRoom();
        await firstRoom.Enter(runState);
        await firstRoom.Engine.EndPlayerTurnAsync();
        await firstRoom.Exit(runState);

        CombatRoom secondRoom = CreateCombatRoom();
        await secondRoom.Enter(runState);

        Assert.Equal(6, player.PlayerCombatState!.Hand.Cards.Count);
    }

    [Fact]
    public async Task PotionBelt_PreservesExistingPotionAndAppendsTwoEmptySlots()
    {
        (_, Player player) = CreateRun("potion-belt");
        PotionModel potion = player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());

        await Obtain<PotionBelt>(player);

        Assert.Equal(5, player.MaxPotionCount);
        Assert.Contains(player.PotionSlots, slot => ReferenceEquals(slot, potion));
        Assert.Equal(4, player.PotionSlots.Count(slot => slot is null));
    }

    [Fact]
    public async Task RedMask_AppliesOneWeakToEveryEnemyBeforeFirstPlayerTurn()
    {
        (RunState runState, Player player) = CreateRun("red-mask");
        await Obtain<RedMask>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        Assert.All(
            room.Engine.State.Enemies,
            enemy => Assert.Equal(1, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount));
    }

    [Fact]
    public async Task RegalPillow_AddsFifteenOnlyToRestSiteHealing()
    {
        (RunState runState, Player player) = CreateRun("regal-pillow");
        await Obtain<RegalPillow>(player);
        player.Creature.LoseHpInternal(player.Creature.MaxHp - 1, ValueProp.Unpowered);
        int beforeOrdinaryHeal = player.Creature.CurrentHp;

        await CreatureCmd.Heal(player.Creature, 5m);
        Assert.Equal(beforeOrdinaryHeal + 5, player.Creature.CurrentHp);

        player.Creature.LoseHpInternal(5m, ValueProp.Unpowered);
        int beforeRest = player.Creature.CurrentHp;
        var restSite = new RestSiteRoom();
        await restSite.Enter(runState);
        await restSite.ResolveAsync(player, new RestSiteDecision.Heal());

        decimal expectedRestHeal = player.Creature.MaxHp * 0.3m + 15m;
        Assert.Equal(
            Math.Min(beforeRest + (int)expectedRestHeal, player.Creature.MaxHp),
            player.Creature.CurrentHp);
    }

    [Fact]
    public async Task RestSiteHealModifiers_FoldDecimalAmountsInListenerOrder()
    {
        (RunState runState, Player player) = CreateRun("rest-heal-fold");
        await Obtain<DoubleRestSiteHealRelic>(player);
        await Obtain<AddFiveRestSiteHealRelic>(player);
        player.Creature.LoseHpInternal(player.Creature.MaxHp - 1, ValueProp.Unpowered);
        var restSite = new RestSiteRoom();
        await restSite.Enter(runState);

        await restSite.ResolveAsync(player, new RestSiteDecision.Heal());

        decimal expectedHeal = player.Creature.MaxHp * 0.3m * 2m + 5m;
        Assert.Equal(1 + (int)expectedHeal, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Strawberry_IncreasesCurrentAndMaximumHpBySeven()
    {
        (_, Player player) = CreateRun("strawberry");
        player.Creature.LoseHpInternal(20m, ValueProp.Unpowered);
        int currentHp = player.Creature.CurrentHp;
        int maxHp = player.Creature.MaxHp;

        await Obtain<Strawberry>(player);

        Assert.Equal(maxHp + 7, player.Creature.MaxHp);
        Assert.Equal(currentHp + 7, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task StrikeDummy_AddsThreeWhenDealerOrCardOwnerMatches()
    {
        (RunState runState, Player player) = CreateRun("strike-dummy");
        await Obtain<StrikeDummy>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        CardModel strike = player.Deck.Cards.OfType<StrikeRegent>().First();
        CardModel untaggedAttack = player.Deck.Cards.OfType<FallingStar>().Single();
        Player foreignPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        CardModel foreignStrike = foreignPlayer.Deck.Cards.OfType<StrikeRegent>().First();
        Sts2Sim.Core.Entities.Creatures.Creature enemy = room.Engine.State.Enemies[0];

        decimal poweredStrike = Hook.ModifyDamage(
            room.Engine.State, enemy, player.Creature, 6m, ValueProp.Move, strike, null, out _);
        decimal unpoweredStrike = Hook.ModifyDamage(
            room.Engine.State, enemy, player.Creature, 6m,
            ValueProp.Move | ValueProp.Unpowered, strike, null, out _);
        decimal untaggedDamage = Hook.ModifyDamage(
            room.Engine.State, enemy, player.Creature, 6m, ValueProp.Move, untaggedAttack, null, out _);
        decimal ownerCardOnly = Hook.ModifyDamage(
            room.Engine.State, enemy, enemy, 6m, ValueProp.Move, strike, null, out _);
        decimal ownerDealerOnly = Hook.ModifyDamage(
            room.Engine.State, enemy, player.Creature, 6m, ValueProp.Move, foreignStrike, null, out _);
        decimal unrelatedSource = Hook.ModifyDamage(
            room.Engine.State, enemy, enemy, 6m, ValueProp.Move, foreignStrike, null, out _);

        Assert.Equal(9m, poweredStrike);
        Assert.Equal(6m, unpoweredStrike);
        Assert.Equal(6m, untaggedDamage);
        Assert.Equal(9m, ownerCardOnly);
        Assert.Equal(9m, ownerDealerOnly);
        Assert.Equal(6m, unrelatedSource);
    }

    [Fact]
    public async Task VenerableTeaSet_StaysArmedAcrossNonCombatRoom_ThenTriggersOnce()
    {
        (RunState runState, Player player) = CreateRun("venerable-tea-set");
        await Obtain<VenerableTeaSet>(player);
        await new RestSiteRoom().Enter(runState);
        await new MerchantRoom().Enter(runState);

        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);

        Assert.Equal(player.MaxEnergy + 2, player.PlayerCombatState!.Energy);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(player.MaxEnergy, player.PlayerCombatState.Energy);
    }

    [Theory]
    [InlineData(typeof(WarPaint), CardType.Skill, 0)]
    [InlineData(typeof(WarPaint), CardType.Skill, 1)]
    [InlineData(typeof(WarPaint), CardType.Skill, 2)]
    [InlineData(typeof(Whetstone), CardType.Attack, 0)]
    [InlineData(typeof(Whetstone), CardType.Attack, 1)]
    [InlineData(typeof(Whetstone), CardType.Attack, 2)]
    public async Task PaintRelics_UpgradeAtMostTwoEligibleCards_AndHandleFewerSafely(
        Type relicType,
        CardType targetType,
        int remainingCandidates)
    {
        (_, Player player) = CreateRun($"paint-{relicType.Name}-{remainingCandidates}");
        CardModel[] targets = player.Deck.Cards.Where(card => card.Type == targetType).ToArray();
        CardModel[] nonTargets = player.Deck.Cards.Where(card => card.Type != targetType).ToArray();
        foreach (CardModel card in targets.Take(targets.Length - remainingCandidates))
        {
            card.Upgrade();
        }

        RelicModel canonical = ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType));
        await RelicCmd.Obtain(canonical, player);

        Assert.All(targets, card => Assert.True(card.IsUpgraded));
        Assert.All(nonTargets, card => Assert.False(card.IsUpgraded));
    }

    private static Task Obtain<TRelic>(Player player)
        where TRelic : RelicModel =>
        RelicCmd.Obtain(ModelDb.Relic<TRelic>(), player);

    private static CombatRoom CreateCombatRoom() =>
        new(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
