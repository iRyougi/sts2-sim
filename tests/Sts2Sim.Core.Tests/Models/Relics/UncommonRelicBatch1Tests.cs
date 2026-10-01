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

file sealed class LifecycleProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public List<string> DamageEvents { get; private set; } = new();

    public List<bool> ExhaustCauses { get; private set; } = new();

    public int DeathCount { get; private set; }

    public override Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        DamageEvents.Add("damage");
        return Task.CompletedTask;
    }

    public override Task AfterDeath(Creature target)
    {
        DamageEvents.Add("death");
        DeathCount++;
        return Task.CompletedTask;
    }

    public override Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        ExhaustCauses.Add(causedByEthereal);
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        DamageEvents = new List<string>(DamageEvents);
        ExhaustCauses = new List<bool>(ExhaustCauses);
    }
}

file sealed class EndTurnBlockProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public int ObservedBlock { get; private set; } = -1;

    public override Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            ObservedBlock = Owner.Creature.Block;
        }

        return Task.CompletedTask;
    }
}

file sealed class ZeroCostAttackProbeCard : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class ZeroCostSkillProbeCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class ExhaustProbeCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };
}

file sealed class EtherealProbeCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Ethereal };
}

file sealed class StarSpendProbeCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override int CanonicalStarCost => 6;
}

[Collection("ModelDb")]
public sealed class UncommonRelicBatch1Tests : IDisposable
{
    public UncommonRelicBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(LifecycleProbeRelic))
                .Append(typeof(EndTurnBlockProbeRelic))
                .Append(typeof(ZeroCostAttackProbeCard))
                .Append(typeof(ZeroCostSkillProbeCard))
                .Append(typeof(ExhaustProbeCard))
                .Append(typeof(EtherealProbeCard))
                .Append(typeof(StarSpendProbeCard)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task DeathHook_SkipsLethalDamageReceived_AndRunsOnceDespiteRepeatedDeadDamage()
    {
        (RunState runState, Player player) = CreateRun("death-hook");
        await Obtain<LifecycleProbeRelic>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        LifecycleProbeRelic probe = Assert.Single(player.Relics.OfType<LifecycleProbeRelic>());
        Creature enemy = room.Engine.State.Enemies[0];

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { enemy },
            1m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            player.Creature,
            null,
            null);

        Assert.Equal(new[] { "damage" }, probe.DamageEvents);
        Assert.Equal(0, probe.DeathCount);

        probe.DamageEvents.Clear();
        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { enemy },
            999m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            player.Creature,
            null,
            null);

        Assert.Equal(new[] { "death" }, probe.DamageEvents);
        Assert.Equal(1, probe.DeathCount);

        probe.DamageEvents.Clear();
        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { enemy },
            999m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            player.Creature,
            null,
            null);

        // 原版 Damage 在钩子前跳过死亡目标，不再派发伤害钩子。
        Assert.Empty(probe.DamageEvents);
        Assert.Equal(1, probe.DeathCount);
    }

    [Fact]
    public async Task GremlinHorn_OnEnemyDeath_GainsOneEnergyAndDrawsOneCard()
    {
        (RunState runState, Player player) = CreateRun("gremlin-horn");
        await Obtain<GremlinHorn>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature victim = Assert.Single(room.Engine.State.Enemies);
        // 留一个敌人在场：原版 CardPileCmd.DrawInternal 在 IsOverOrEnding 时不抽牌，
        // 打死最后一个敌人时 Gremlin Horn 只加能量、不抽牌。
        await CreatureCmd.Add(
            (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            room.Engine.State,
            CombatSide.Enemy,
            null);
        int energyBefore = player.PlayerCombatState!.Energy;
        int handBefore = player.PlayerCombatState.Hand.Cards.Count;

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { victim },
            999m,
            ValueProp.Unblockable | ValueProp.Unpowered,
            player.Creature,
            null,
            null);

        Assert.False(room.Engine.State.IsOverOrEnding());
        Assert.Equal(energyBefore + 1, player.PlayerCombatState.Energy);
        Assert.Equal(handBefore + 1, player.PlayerCombatState.Hand.Cards.Count);
    }

    [Fact]
    public async Task ExhaustHook_CoversPlayedEtherealAndPurityExhausts_ButNotGeneratedCards()
    {
        (RunState runState, Player player) = CreateRun("exhaust-hook");
        await Obtain<LifecycleProbeRelic>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        LifecycleProbeRelic probe = Assert.Single(player.Relics.OfType<LifecycleProbeRelic>());

        ExhaustProbeCard played = AddToHand<ExhaustProbeCard>(player);
        await room.Engine.PlayCardAsync(player, played, null);

        AddToHand<EtherealProbeCard>(player);
        await room.Engine.EndPlayerTurnAsync();

        var selection = new LegacySelectionDecisionSource();
        room.Engine.State.CardSelectionSource = selection;
        Purity purity = AddToHand<Purity>(player);
        await room.Engine.PlayCardAsync(player, purity, null);

        ExhaustProbeCard generated = CreateOwned<ExhaustProbeCard>(player);
        await CardPileCmd.Generate(room.Engine.State, generated, PileType.Exhaust);

        Assert.Equal(
            new[] { false, true, false, false, false, false },
            probe.ExhaustCauses);
    }

    [Fact]
    public async Task JossPaper_DrawsAfterFiveNormalExhausts()
    {
        (RunState runState, Player player) = CreateRun("joss-normal");
        await Obtain<JossPaper>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;

        for (int i = 0; i < 5; i++)
        {
            await room.Engine.PlayCardAsync(player, AddToHand<ExhaustProbeCard>(player), null);
        }

        Assert.Equal(handBefore + 1, player.PlayerCombatState.Hand.Cards.Count);
    }

    [Fact]
    public async Task JossPaper_DefersEtherealDrawUntilAfterFlush()
    {
        (RunState runState, Player player) = CreateRun("joss-ethereal");
        await Obtain<JossPaper>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        for (int i = 0; i < 5; i++)
        {
            AddToHand<EtherealProbeCard>(player);
        }

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(6, player.PlayerCombatState!.Hand.Cards.Count);
    }

    [Fact]
    public async Task JossPaper_CombatEndClearsPendingEtherealCount()
    {
        (RunState runState, Player player) = CreateRun("joss-cleanup");
        await Obtain<JossPaper>(player);
        CombatRoom firstRoom = CreateCombatRoom();
        await firstRoom.Enter(runState);
        JossPaper joss = Assert.Single(player.Relics.OfType<JossPaper>());
        EtherealProbeCard card = CreateOwned<EtherealProbeCard>(player);
        for (int i = 0; i < 4; i++)
        {
            await joss.AfterCardExhausted(card, causedByEthereal: true);
        }

        await firstRoom.Exit(runState);
        CombatRoom secondRoom = CreateCombatRoom();
        await secondRoom.Enter(runState);
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;

        await joss.AfterCardExhausted(card, causedByEthereal: false);

        Assert.Equal(handBefore, player.PlayerCombatState.Hand.Cards.Count);
    }

    [Fact]
    public async Task LuckyFysh_UsesRewardAndMerchantDeckEntrypoints_ButNotUpgrade()
    {
        (RunState runState, Player player) = CreateRun("lucky-fysh");
        await Obtain<LuckyFysh>(player);
        LuckyFysh luckyFysh = Assert.Single(player.Relics.OfType<LuckyFysh>());
        Assert.False(luckyFysh.IsAllowedInShops);
        int initialGold = player.Gold;
        var reward = new CardReward(player, CardRarityOddsType.RegularEncounter);
        reward.Populate(runState);

        await reward.SelectOption(reward.Options[0]);

        Assert.Equal(initialGold + 15, player.Gold);
        CardModel upgradable = player.Deck.Cards.First(card => card.IsUpgradable);
        CardCmd.Upgrade(upgradable);
        Assert.Equal(initialGold + 15, player.Gold);

        player.Gold = 99_999;
        var merchant = new MerchantRoom();
        await merchant.EnterInternal(runState);
        int price = merchant.Inventory.Cards[0].Price;

        await merchant.Buy(merchant.Inventory.Cards[0], player);

        Assert.Equal(99_999 - price + 15, player.Gold);
    }

    [Fact]
    public async Task OpeningRelics_ApplyAkabekoVigorAndMercuryDamageEveryPlayerTurn()
    {
        (RunState runState, Player player) = CreateRun("opening-uncommon");
        await Obtain<Akabeko>(player);
        await Obtain<MercuryHourglass>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        int enemyMaxHp = room.Engine.State.Enemies[0].MaxHp;

        Assert.Equal(8, Assert.Single(player.Creature.Powers.OfType<VigorPower>()).Amount);
        Assert.Equal(enemyMaxHp - 3, room.Engine.State.Enemies[0].CurrentHp);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(8, Assert.Single(player.Creature.Powers.OfType<VigorPower>()).Amount);
        Assert.Equal(enemyMaxHp - 6, room.Engine.State.Enemies[0].CurrentHp);
    }

    [Fact]
    public async Task SecondTurnRelics_GrantCandelabraEnergyAndHornCleatBlock()
    {
        (RunState runState, Player player) = CreateRun("second-turn-uncommon");
        await Obtain<Candelabra>(player);
        await Obtain<HornCleat>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(player.MaxEnergy + 2, player.PlayerCombatState!.Energy);
        Assert.Equal(14, player.Creature.Block);
    }

    [Fact]
    public async Task EternalFeather_HealsThreePerCompleteFiveDeckCardsOnRestEntry()
    {
        (RunState runState, Player player) = CreateRun("eternal-feather");
        await Obtain<EternalFeather>(player);
        player.Creature.LoseHpInternal(30m, ValueProp.Unpowered);
        int hpBefore = player.Creature.CurrentHp;
        int expectedHeal = 3 * (player.Deck.Cards.Count / 5);

        await new RestSiteRoom().Enter(runState);

        Assert.Equal(hpBefore + expectedHeal, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task EternalFeather_DoesNotReviveDeadOwner()
    {
        (RunState runState, Player player) = CreateRun("eternal-feather-dead-owner");
        await Obtain<EternalFeather>(player);
        player.Creature.LoseHpInternal(player.Creature.CurrentHp, ValueProp.Unpowered);
        Assert.False(player.Creature.IsAlive);

        await new RestSiteRoom().Enter(runState);

        Assert.False(player.Creature.IsAlive);
        Assert.Equal(0, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task GalacticDust_AccumulatesStarsAndGrantsTenBlockPerThreshold()
    {
        (RunState runState, Player player) = CreateRun("galactic-dust");
        await Obtain<GalacticDust>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        await PlayerCmd.GainStars(18m, player);

        await room.Engine.PlayCardAsync(player, AddToHand<StarSpendProbeCard>(player), null);
        Assert.Equal(0, player.Creature.Block);

        await room.Engine.PlayCardAsync(player, AddToHand<StarSpendProbeCard>(player), null);
        Assert.Equal(10, player.Creature.Block);

        await room.Engine.PlayCardAsync(player, AddToHand<StarSpendProbeCard>(player), null);
        Assert.Equal(10, player.Creature.Block);
    }

    [Fact]
    public async Task BowlerHat_MultipliesOwnedGoldGainByOnePointTwoFive()
    {
        (_, Player player) = CreateRun("bowler-hat");
        await Obtain<BowlerHat>(player);
        BowlerHat bowlerHat = Assert.Single(player.Relics.OfType<BowlerHat>());
        Assert.False(bowlerHat.IsAllowedInShops);
        int goldBefore = player.Gold;

        await PlayerCmd.GainGold(100m, player);

        Assert.Equal(goldBefore + 125, player.Gold);
    }

    [Fact]
    public async Task CardPlayCounters_TriggerAtTheirExactAttackAndSkillThresholds()
    {
        (RunState runState, Player player) = CreateRun("card-play-counters");
        await Obtain<Kusarigama>(player);
        await Obtain<LetterOpener>(player);
        await Obtain<Nunchaku>(player);
        await Obtain<OrnamentalFan>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];
        int enemyHpBefore = enemy.CurrentHp;
        int energyBefore = player.PlayerCombatState!.Energy;

        await PlayCopies<ZeroCostAttackProbeCard>(room, player, enemy, 10);

        Assert.Equal(enemyHpBefore - 18, enemy.CurrentHp);
        Assert.Equal(12, player.Creature.Block);
        Assert.Equal(energyBefore + 1, player.PlayerCombatState.Energy);

        int hpBeforeSkills = enemy.CurrentHp;
        await PlayCopies<ZeroCostSkillProbeCard>(room, player, null, 6);

        Assert.Equal(hpBeforeSkills - 10, enemy.CurrentHp);
    }

    [Fact]
    public async Task Nunchaku_CounterPersistsAcrossCombatBoundaries()
    {
        (RunState runState, Player player) = CreateRun("nunchaku-persistent");
        await Obtain<Nunchaku>(player);
        CombatRoom firstRoom = CreateCombatRoom();
        await firstRoom.Enter(runState);
        await PlayCopies<ZeroCostAttackProbeCard>(
            firstRoom,
            player,
            firstRoom.Engine.State.Enemies[0],
            9);
        await firstRoom.Exit(runState);

        CombatRoom secondRoom = CreateCombatRoom();
        await secondRoom.Enter(runState);
        int energyBefore = player.PlayerCombatState!.Energy;
        await PlayCopies<ZeroCostAttackProbeCard>(
            secondRoom,
            player,
            secondRoom.Engine.State.Enemies[0],
            1);

        Assert.Equal(energyBefore + 1, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task MiniatureCannon_AddsThreeOnlyToPoweredDamageFromUpgradedOwnedCards()
    {
        (RunState runState, Player player) = CreateRun("miniature-cannon");
        await Obtain<MiniatureCannon>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        StrikeRegent normal = player.Deck.Cards.OfType<StrikeRegent>().First();
        StrikeRegent upgraded = player.Deck.Cards.OfType<StrikeRegent>().Skip(1).First();
        CardCmd.Upgrade(upgraded);
        Creature enemy = room.Engine.State.Enemies[0];

        decimal normalDamage = Hook.ModifyDamage(
            room.Engine.State, enemy, player.Creature, 6m, ValueProp.Move, normal, null, out _);
        decimal upgradedDamage = Hook.ModifyDamage(
            room.Engine.State, enemy, player.Creature, 6m, ValueProp.Move, upgraded, null, out _);
        decimal unpoweredDamage = Hook.ModifyDamage(
            room.Engine.State,
            enemy,
            player.Creature,
            6m,
            ValueProp.Move | ValueProp.Unpowered,
            upgraded,
            null,
            out _);

        Assert.Equal(6m, normalDamage);
        Assert.Equal(9m, upgradedDamage);
        Assert.Equal(6m, unpoweredDamage);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 5)]
    public async Task Orichalcum_UsesRealEndTurnPhasesBeforePlatingPower(
        int startingBlock,
        int expectedObservedBlock)
    {
        (RunState runState, Player player) = CreateRun($"orichalcum-{startingBlock}");
        await Obtain<Orichalcum>(player);
        await Obtain<EndTurnBlockProbeRelic>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        await PowerCmd.Apply<PlatingPower>(
            room.Engine.State,
            player.Creature,
            4m,
            player.Creature,
            null);
        player.Creature.GainBlockInternal(startingBlock);

        await room.Engine.EndPlayerTurnAsync();

        EndTurnBlockProbeRelic probe =
            Assert.Single(player.Relics.OfType<EndTurnBlockProbeRelic>());
        Assert.Equal(expectedObservedBlock, probe.ObservedBlock);
    }

    private static Task Obtain<TRelic>(Player player)
        where TRelic : RelicModel =>
        RelicCmd.Obtain(ModelDb.Relic<TRelic>(), player);

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        TCard card = CreateOwned<TCard>(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static TCard CreateOwned<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static async Task PlayCopies<TCard>(
        CombatRoom room,
        Player player,
        Creature? target,
        int count)
        where TCard : CardModel
    {
        for (int i = 0; i < count; i++)
        {
            await room.Engine.PlayCardAsync(player, AddToHand<TCard>(player), target);
        }
    }

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
