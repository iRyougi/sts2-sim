using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
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

file static class Task10CardSpecs
{
    public static GeneratedCardSpec Attack(bool xEnergy, bool xStars) => new(
        EnergyCost: 0,
        StarCost: 0,
        Type: CardType.Attack,
        Rarity: CardRarity.Token,
        TargetType: TargetType.AnyEnemy,
        IsColorless: false,
        IsXEnergyCost: xEnergy,
        IsXStarCost: xStars,
        Keywords: Array.Empty<CardKeyword>(),
        Damage: 1m,
        HitCount: xEnergy || xStars ? 0 : 1,
        Block: 0m,
        Draw: 0,
        GainEnergy: 0,
        GainStars: 0,
        Weak: 0m,
        Vulnerable: 0m,
        Strength: 0m,
        Vigor: 0m,
        Forge: 0m,
        UpgradeDamage: 0m,
        UpgradeBlock: 0m,
        UpgradeDraw: 0,
        UpgradeStars: 0,
        UpgradeVigor: 0m,
        UpgradeForge: 0m,
        ReduceCost: false,
        AddKeyword: null,
        RemoveKeyword: null);
}

file sealed class Task10XEnergyAttackCard : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } =
        Task10CardSpecs.Attack(xEnergy: true, xStars: false);
}

file sealed class Task10XStarAttackCard : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } =
        Task10CardSpecs.Attack(xEnergy: false, xStars: true);
}

file sealed class Task10FixedAttackCard : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } =
        Task10CardSpecs.Attack(xEnergy: false, xStars: false);
}

file sealed class Task10ExhaustSkillCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };
}

file sealed class Task10ExhaustAttackCard : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };
}

file sealed class Task10CommonStrikeCard : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardTag> CanonicalTags =>
        new[] { CardTag.Strike };
}

file sealed class Task10BasicUntaggedCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task10QuestCard : CardModel
{
    public override CardType Type => CardType.Quest;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task10MirrorEligibleCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;
}

[Collection("ModelDb")]
public sealed class ShopRelicBatch1Tests : IDisposable
{
    private static readonly Type[] TestCardTypes =
    {
        typeof(Task10XEnergyAttackCard),
        typeof(Task10XStarAttackCard),
        typeof(Task10FixedAttackCard),
        typeof(Task10ExhaustSkillCard),
        typeof(Task10ExhaustAttackCard),
        typeof(Task10CommonStrikeCard),
        typeof(Task10BasicUntaggedCard),
        typeof(Task10QuestCard),
        typeof(Task10MirrorEligibleCard),
    };

    public ShopRelicBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(TestCardTypes));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Task10Relics_RegistersExactlyTheEightShopModels()
    {
        Type[] expectedTypes =
        {
            typeof(BeltBuckle),
            typeof(Bread),
            typeof(BurningSticks),
            typeof(ChemicalX),
            typeof(DollysMirror),
            typeof(DragonFruit),
            typeof(GhostSeed),
            typeof(LeesWaffle),
        };

        RelicModel[] relics = expectedTypes
            .Select(type => ModelDb.GetById<RelicModel>(ModelDb.GetId(type)))
            .ToArray();

        Assert.Equal(8, expectedTypes.Length);
        Assert.Equal(8, relics.Select(relic => relic.GetType()).Distinct().Count());
        Assert.Equal(
            expectedTypes.OrderBy(type => type.FullName),
            relics.Select(relic => relic.GetType()).OrderBy(type => type.FullName));
        Assert.All(relics, relic => Assert.Equal(RelicRarity.Shop, relic.Rarity));
    }

    [Fact]
    public async Task ChemicalX_AddsTwoToEnergyAndStarXValuesWithoutIncreasingResourcesSpent()
    {
        (RunState runState, Player player) = CreateRun("chemical-x-both-resources");
        await Obtain<ChemicalX>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        int hpBeforeEnergyX = enemy.CurrentHp;
        int energyBefore = player.PlayerCombatState!.Energy;
        await room.Engine.PlayCardAsync(
            player,
            AddToPile<Task10XEnergyAttackCard>(player, PileType.Hand),
            enemy);

        Assert.Equal(hpBeforeEnergyX - energyBefore - 2, enemy.CurrentHp);
        Assert.Equal(0, player.PlayerCombatState.Energy);

        await PlayerCmd.GainStars(2m, player);
        int hpBeforeStarX = enemy.CurrentHp;
        int starsBefore = player.PlayerCombatState.Stars;
        await room.Engine.PlayCardAsync(
            player,
            AddToPile<Task10XStarAttackCard>(player, PileType.Hand),
            enemy);

        Assert.Equal(hpBeforeStarX - starsBefore - 2, enemy.CurrentHp);
        Assert.Equal(0, player.PlayerCombatState.Stars);
    }

    [Fact]
    public async Task ChemicalX_IgnoresNonXCardsAndCardsOwnedByAnotherPlayer()
    {
        (RunState runState, Player owner, Player other) =
            CreateTwoPlayerRun("chemical-x-ownership");
        await Obtain<ChemicalX>(owner);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];

        int hpBeforeFixed = enemy.CurrentHp;
        await room.Engine.PlayCardAsync(
            owner,
            AddToPile<Task10FixedAttackCard>(owner, PileType.Hand),
            enemy);
        Assert.Equal(hpBeforeFixed - 1, enemy.CurrentHp);

        int hpBeforeForeignX = enemy.CurrentHp;
        int foreignEnergy = other.PlayerCombatState!.Energy;
        await room.Engine.PlayCardAsync(
            other,
            AddToPile<Task10XEnergyAttackCard>(other, PileType.Hand),
            enemy);

        Assert.Equal(hpBeforeForeignX - foreignEnergy, enemy.CurrentHp);
        Assert.Equal(0, other.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task BeltBuckle_ZeroPotionCombatAppliesExactlyTwoDexterityAndResetsNextCombat()
    {
        (RunState runState, Player player) = CreateRun("belt-buckle-reset");
        await Obtain<BeltBuckle>(player);
        CombatRoom firstRoom = CreateCombatRoom();
        await firstRoom.Enter(runState);

        Assert.Equal(
            2,
            Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);

        await firstRoom.Exit(runState);

        Assert.Empty(player.Creature.Powers.OfType<DexterityPower>());
        Assert.Null(player.PlayerCombatState);

        CombatRoom secondRoom = CreateCombatRoom();
        await secondRoom.Enter(runState);

        Assert.Equal(
            2,
            Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);
    }

    [Fact]
    public async Task BeltBuckle_ReconcilesDelayedPotionProcurementAtNextPotionUse()
    {
        (RunState runState, Player player) =
            CreateRun("belt-buckle-delayed-procurement");
        await Obtain<BeltBuckle>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);

        Assert.Equal(
            2,
            Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);

        PotionModel firstPotion =
            player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        _ = player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());

        Assert.Equal(
            2,
            Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);

        await PotionCmd.Use(firstPotion, player, player.Creature);

        Assert.Single(player.PotionSlots, potion => potion is not null);
        Assert.Empty(player.Creature.Powers.OfType<DexterityPower>());
    }

    [Fact]
    public async Task BeltBuckle_ProcurementAndDiscardReconcileBeforeCommandsComplete()
    {
        (RunState runState, Player player) = CreateRun("belt-buckle-procure-discard");
        await Obtain<BeltBuckle>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Assert.Equal(2, player.Creature.GetPower<DexterityPower>()!.Amount);

        var potion = (PotionModel)ModelDb.Potion<StrengthPotion>().MutableClone();
        Assert.True(await PotionCmd.TryToProcure(potion, player));
        Assert.Null(player.Creature.GetPower<DexterityPower>());
        await PotionCmd.Discard(potion);
        Assert.Equal(2, player.Creature.GetPower<DexterityPower>()!.Amount);

        Assert.True(await PotionCmd.TryToProcure(potion, player));
        Assert.Null(player.Creature.GetPower<DexterityPower>());
        await PotionCmd.DiscardForEvent(potion);
        Assert.Equal(2, player.Creature.GetPower<DexterityPower>()!.Amount);
        await room.Exit(runState);
        Assert.True(await PotionCmd.TryToProcure(potion, player));
        await PotionCmd.Discard(potion);
        Assert.Empty(player.Creature.Powers);
    }
    [Fact]
    public async Task BeltBuckle_AppliesOnlyAfterTheLastHeldPotionIsUsed()
    {
        (RunState runState, Player player) = CreateRun("belt-buckle-last-potion");
        await Obtain<BeltBuckle>(player);
        PotionModel firstPotion = player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        PotionModel secondPotion = player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);

        Assert.Empty(player.Creature.Powers.OfType<DexterityPower>());

        await PotionCmd.Use(firstPotion, player, player.Creature);

        Assert.Single(player.PotionSlots, potion => potion is not null);
        Assert.Empty(player.Creature.Powers.OfType<DexterityPower>());

        await PotionCmd.Use(secondPotion, player, player.Creature);

        Assert.DoesNotContain(player.PotionSlots, potion => potion is not null);
        Assert.Equal(
            2,
            Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);
    }

    [Fact]
    public async Task BurningSticks_CopiesOnlyTheFirstOwnedSkillExhaustAndResetsNextCombat()
    {
        (RunState runState, Player player) = CreateRun("burning-sticks-reset");
        await Obtain<BurningSticks>(player);
        CombatRoom firstRoom = CreateCombatRoom();
        await firstRoom.Enter(runState);
        ClearHand(player);

        Task10ExhaustAttackCard attack =
            AddToPile<Task10ExhaustAttackCard>(player, PileType.Hand);
        await CardPileCmd.Exhaust(firstRoom.Engine.State, attack);
        Assert.Empty(player.PlayerCombatState!.Hand.Cards.OfType<Task10ExhaustAttackCard>());

        Task10ExhaustSkillCard firstSkill =
            AddToPile<Task10ExhaustSkillCard>(player, PileType.Hand);
        await CardPileCmd.Exhaust(
            firstRoom.Engine.State,
            firstSkill,
            causedByEthereal: true);

        Task10ExhaustSkillCard firstCopy =
            Assert.Single(player.PlayerCombatState.Hand.Cards.OfType<Task10ExhaustSkillCard>());
        Assert.NotSame(firstSkill, firstCopy);
        Assert.Equal(PileType.Exhaust, firstSkill.Pile!.Type);

        Task10ExhaustSkillCard secondSkill =
            AddToPile<Task10ExhaustSkillCard>(player, PileType.Hand);
        await CardPileCmd.Exhaust(firstRoom.Engine.State, secondSkill);

        Assert.Same(
            firstCopy,
            Assert.Single(player.PlayerCombatState.Hand.Cards.OfType<Task10ExhaustSkillCard>()));

        await firstRoom.Exit(runState);
        CombatRoom secondRoom = CreateCombatRoom();
        await secondRoom.Enter(runState);
        ClearHand(player);
        Task10ExhaustSkillCard nextCombatSkill =
            AddToPile<Task10ExhaustSkillCard>(player, PileType.Hand);

        await CardPileCmd.Exhaust(secondRoom.Engine.State, nextCombatSkill);

        Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<Task10ExhaustSkillCard>());
    }

    [Fact]
    public async Task BurningSticks_DoesNotConsumeItsTriggerForAnotherPlayersSkill()
    {
        (RunState runState, Player owner, Player other) =
            CreateTwoPlayerRun("burning-sticks-ownership");
        await Obtain<BurningSticks>(owner);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(owner);
        ClearHand(other);

        Task10ExhaustSkillCard foreignSkill =
            AddToPile<Task10ExhaustSkillCard>(other, PileType.Hand);
        await CardPileCmd.Exhaust(room.Engine.State, foreignSkill);

        Assert.Empty(owner.PlayerCombatState!.Hand.Cards.OfType<Task10ExhaustSkillCard>());
        Assert.Empty(other.PlayerCombatState!.Hand.Cards.OfType<Task10ExhaustSkillCard>());

        Task10ExhaustSkillCard ownedSkill =
            AddToPile<Task10ExhaustSkillCard>(owner, PileType.Hand);
        await CardPileCmd.Exhaust(room.Engine.State, ownedSkill);

        Assert.Single(owner.PlayerCombatState.Hand.Cards.OfType<Task10ExhaustSkillCard>());
    }

    [Fact]
    public async Task GhostSeed_CombatRoomEntryMakesEveryBasicStrikeAndDefendEthereal()
    {
        (RunState runState, Player player) = CreateRun("ghost-seed-entry");
        await Obtain<GhostSeed>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        CardModel[] qualifyingCards = player.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .Where(IsBasicStrikeOrDefend)
            .ToArray();
        Assert.NotEmpty(qualifyingCards);
        Assert.All(
            qualifyingCards,
            card => Assert.True(card.HasKeyword(CardKeyword.Ethereal)));
    }

    [Fact]
    public async Task GhostSeed_ScansAllFiveOwnedCombatPilesAndRejectsNearMisses()
    {
        (RunState runState, Player owner, Player other) =
            CreateTwoPlayerRun("ghost-seed-all-piles");
        await Obtain<GhostSeed>(owner);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        GhostSeed ghostSeed = Assert.Single(owner.Relics.OfType<GhostSeed>());
        var qualifyingCards = new List<CardModel>();

        foreach (PileType pileType in new[]
                 {
                     PileType.Hand,
                     PileType.Draw,
                     PileType.Discard,
                     PileType.Exhaust,
                     PileType.Play,
                 })
        {
            qualifyingCards.Add(AddToPile<StrikeRegent>(owner, pileType));
            qualifyingCards.Add(AddToPile<DefendRegent>(owner, pileType));
        }

        Task10CommonStrikeCard wrongRarity =
            AddToPile<Task10CommonStrikeCard>(owner, PileType.Discard);
        Task10BasicUntaggedCard wrongTag =
            AddToPile<Task10BasicUntaggedCard>(owner, PileType.Draw);
        StrikeRegent foreignOwned = CreateOwned<StrikeRegent>(other);
        owner.PlayerCombatState!.DrawPile.AddInternal(foreignOwned);

        await ghostSeed.AfterRoomEntered(room);

        Assert.All(
            qualifyingCards,
            card => Assert.True(card.HasKeyword(CardKeyword.Ethereal)));
        Assert.False(wrongRarity.HasKeyword(CardKeyword.Ethereal));
        Assert.False(wrongTag.HasKeyword(CardKeyword.Ethereal));
        Assert.False(foreignOwned.HasKeyword(CardKeyword.Ethereal));
    }

    [Fact]
    public async Task GhostSeed_DoesNotModifyThePermanentDeckInANonCombatRoom()
    {
        (_, Player player) = CreateRun("ghost-seed-noncombat");
        await Obtain<GhostSeed>(player);
        GhostSeed ghostSeed = Assert.Single(player.Relics.OfType<GhostSeed>());
        CardModel[] qualifyingDeckCards =
            player.Deck.Cards.Where(IsBasicStrikeOrDefend).ToArray();
        Assert.NotEmpty(qualifyingDeckCards);

        await ghostSeed.AfterRoomEntered(new RestSiteRoom());

        Assert.All(
            qualifyingDeckCards,
            card => Assert.False(card.HasKeyword(CardKeyword.Ethereal)));
    }

    [Fact]
    public async Task DollysMirror_ClonesExplicitlySelectedFirstNonQuestDeckCard()
    {
        (_, Player player) = CreateRun("dollys-mirror");
        Task10MirrorEligibleCard eligible = CreateOwned<Task10MirrorEligibleCard>(player);
        Task10QuestCard quest = CreateOwned<Task10QuestCard>(player);
        player.Deck.AddInternal(eligible, index: 0);
        player.Deck.AddInternal(quest, index: 0);
        int countBefore = player.Deck.Cards.Count;

        var selection = new LegacySelectionDecisionSource();
        ((RunState)player.RunState).ConfigureCardSelectionSource(selection);
        await Obtain<DollysMirror>(player);
        Assert.Same(eligible, Assert.Single(selection.Requests).Candidates[0]);

        DollysMirror mirror = Assert.Single(player.Relics.OfType<DollysMirror>());
        Task10MirrorEligibleCard[] eligibleCards =
            player.Deck.Cards.OfType<Task10MirrorEligibleCard>().ToArray();
        Assert.True(mirror.HasUponPickupEffect);
        Assert.Equal(countBefore + 1, player.Deck.Cards.Count);
        Assert.Equal(2, eligibleCards.Length);
        Assert.Contains(eligible, eligibleCards);
        Assert.NotSame(eligible, eligibleCards.Single(card => !ReferenceEquals(card, eligible)));
        Assert.IsType<Task10MirrorEligibleCard>(player.Deck.Cards[^1]);
        Assert.Single(player.Deck.Cards.OfType<Task10QuestCard>());
    }

    [Fact]
    public async Task DragonFruit_GainsOneMaxAndCurrentHpOnlyAfterItsOwnerGainsGold()
    {
        (_, Player owner, Player other) = CreateTwoPlayerRun("dragon-fruit");
        owner.Creature.LoseHpInternal(10m, ValueProp.Unpowered);
        int maxHpBefore = owner.Creature.MaxHp;
        int currentHpBefore = owner.Creature.CurrentHp;

        await Obtain<DragonFruit>(owner);

        DragonFruit dragonFruit = Assert.Single(owner.Relics.OfType<DragonFruit>());
        Assert.False(dragonFruit.HasUponPickupEffect);
        Assert.Equal(maxHpBefore, owner.Creature.MaxHp);
        Assert.Equal(currentHpBefore, owner.Creature.CurrentHp);

        await PlayerCmd.GainGold(25m, other);

        Assert.Equal(maxHpBefore, owner.Creature.MaxHp);
        Assert.Equal(currentHpBefore, owner.Creature.CurrentHp);

        await PlayerCmd.GainGold(25m, owner);

        Assert.Equal(maxHpBefore + 1, owner.Creature.MaxHp);
        Assert.Equal(currentHpBefore + 1, owner.Creature.CurrentHp);
    }

    [Fact]
    public async Task LeesWaffle_IncreasesMaxHpBeforeHealingToTheNewFullAmount()
    {
        (_, Player player) = CreateRun("lees-waffle");
        player.Creature.LoseHpInternal(20m, ValueProp.Unpowered);
        int maxHpBefore = player.Creature.MaxHp;

        await Obtain<LeesWaffle>(player);

        LeesWaffle waffle = Assert.Single(player.Relics.OfType<LeesWaffle>());
        Assert.True(waffle.HasUponPickupEffect);
        Assert.Equal(maxHpBefore + 7, player.Creature.MaxHp);
        Assert.Equal(player.Creature.MaxHp, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task Bread_UsesFirstTurnPenaltyThenOwnedPlusOneMaxEnergyOnLaterTurns()
    {
        (RunState runState, Player owner, Player other) =
            CreateTwoPlayerRun("bread-turns");
        await Obtain<Bread>(owner);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        Assert.Equal(1, owner.PlayerCombatState!.TurnNumber);
        Assert.Equal(owner.MaxEnergy, owner.PlayerCombatState.MaxEnergy);
        Assert.Equal(owner.MaxEnergy - 2, owner.PlayerCombatState.Energy);
        Assert.Equal(other.MaxEnergy, other.PlayerCombatState!.MaxEnergy);
        Assert.Equal(other.MaxEnergy, other.PlayerCombatState.Energy);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, owner.PlayerCombatState.TurnNumber);
        Assert.Equal(owner.MaxEnergy + 1, owner.PlayerCombatState.MaxEnergy);
        Assert.Equal(owner.MaxEnergy + 1, owner.PlayerCombatState.Energy);
        Assert.Equal(other.MaxEnergy, other.PlayerCombatState!.MaxEnergy);
        Assert.Equal(other.MaxEnergy, other.PlayerCombatState.Energy);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(3, owner.PlayerCombatState.TurnNumber);
        Assert.Equal(owner.MaxEnergy + 1, owner.PlayerCombatState.MaxEnergy);
        Assert.Equal(owner.MaxEnergy + 1, owner.PlayerCombatState.Energy);
    }

    private static bool IsBasicStrikeOrDefend(CardModel card) =>
        card.Rarity == CardRarity.Basic &&
        (card.Tags.Contains(CardTag.Strike) || card.Tags.Contains(CardTag.Defend));

    private static Task Obtain<TRelic>(Player player)
        where TRelic : RelicModel =>
        RelicCmd.Obtain(ModelDb.Relic<TRelic>(), player);

    private static TCard AddToPile<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        TCard card = CreateOwned<TCard>(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private static TCard CreateOwned<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Discard);
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

    private static (RunState RunState, Player Owner, Player Other)
        CreateTwoPlayerRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(other);
        return (runState, owner, other);
    }
}
