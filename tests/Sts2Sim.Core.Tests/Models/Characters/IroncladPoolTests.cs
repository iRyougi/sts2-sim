using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class IroncladPoolTests : IDisposable
{
    // Transcribed from v0.111.0 IroncladCardPool.GenerateAllCards and each card constructor.
    // These expectations deliberately do not read IroncladCardPool's production type table.
    private static readonly (Type Type, CardRarity Rarity, int Cost)[] SourceCards =
    [
        (typeof(Aggression), CardRarity.Rare, 1),
        (typeof(Anger), CardRarity.Common, 0),
        (typeof(Armaments), CardRarity.Common, 1),
        (typeof(AshenStrike), CardRarity.Uncommon, 1),
        (typeof(Barricade), CardRarity.Rare, 3),
        (typeof(Bash), CardRarity.Basic, 2),
        (typeof(BattleTrance), CardRarity.Uncommon, 0),
        (typeof(Blaze), CardRarity.Uncommon, 2),
        (typeof(BloodWall), CardRarity.Common, 2),
        (typeof(Bloodletting), CardRarity.Uncommon, 0),
        (typeof(Bludgeon), CardRarity.Uncommon, 3),
        (typeof(BodySlam), CardRarity.Common, 1),
        (typeof(Brand), CardRarity.Rare, 0),
        (typeof(Break), CardRarity.Ancient, 1),
        (typeof(Breakthrough), CardRarity.Common, 1),
        (typeof(Bully), CardRarity.Uncommon, 0),
        (typeof(BurningPact), CardRarity.Uncommon, 1),
        (typeof(Cascade), CardRarity.Rare, -1),
        (typeof(Cinder), CardRarity.Common, 2),
        (typeof(Colossus), CardRarity.Uncommon, 1),
        (typeof(Conflagration), CardRarity.Rare, 1),
        (typeof(Corruption), CardRarity.Ancient, 3),
        (typeof(CrimsonMantle), CardRarity.Rare, 1),
        (typeof(Cruelty), CardRarity.Uncommon, 1),
        (typeof(DarkEmbrace), CardRarity.Rare, 2),
        (typeof(DefendIronclad), CardRarity.Basic, 1),
        (typeof(DemonForm), CardRarity.Rare, 3),
        (typeof(DemonicShield), CardRarity.Uncommon, 0),
        (typeof(Dismantle), CardRarity.Uncommon, 1),
        (typeof(Dominate), CardRarity.Rare, 1),
        (typeof(DrumOfBattle), CardRarity.Uncommon, 1),
        (typeof(EvilEye), CardRarity.Uncommon, 1),
        (typeof(ExpectAFight), CardRarity.Uncommon, 3),
        (typeof(Feed), CardRarity.Rare, 1),
        (typeof(FeelNoPain), CardRarity.Uncommon, 1),
        (typeof(FiendFire), CardRarity.Rare, 2),
        (typeof(FightMe), CardRarity.Uncommon, 2),
        (typeof(FlameBarrier), CardRarity.Uncommon, 2),
        (typeof(ForgottenRitual), CardRarity.Uncommon, 1),
        (typeof(Havoc), CardRarity.Common, 1),
        (typeof(Headbutt), CardRarity.Common, 1),
        (typeof(Hellraiser), CardRarity.Rare, 2),
        (typeof(Hemokinesis), CardRarity.Uncommon, 1),
        (typeof(HowlFromBeyond), CardRarity.Uncommon, 3),
        (typeof(Impervious), CardRarity.Rare, 2),
        (typeof(InfernalBlade), CardRarity.Uncommon, 1),
        (typeof(Inferno), CardRarity.Uncommon, 1),
        (typeof(Inflame), CardRarity.Uncommon, 1),
        (typeof(IronWave), CardRarity.Common, 1),
        (typeof(Juggernaut), CardRarity.Rare, 2),
        (typeof(Juggling), CardRarity.Uncommon, 1),
        (typeof(Mangle), CardRarity.Rare, 3),
        (typeof(Midnight), CardRarity.Rare, 12),
        (typeof(MoltenFist), CardRarity.Common, 1),
        (typeof(NotYet), CardRarity.Rare, 2),
        (typeof(Offering), CardRarity.Rare, 0),
        (typeof(OneTwoPunch), CardRarity.Rare, 1),
        (typeof(Outrage), CardRarity.Uncommon, 0),
        (typeof(PactsEnd), CardRarity.Rare, 0),
        (typeof(PerfectedStrike), CardRarity.Common, 2),
        (typeof(Pillage), CardRarity.Uncommon, 1),
        (typeof(PommelStrike), CardRarity.Common, 1),
        (typeof(PrimalForce), CardRarity.Rare, 0),
        (typeof(Pyre), CardRarity.Rare, 2),
        (typeof(Rage), CardRarity.Uncommon, 0),
        (typeof(Rampage), CardRarity.Uncommon, 1),
        (typeof(Rupture), CardRarity.Uncommon, 1),
        (typeof(SecondWind), CardRarity.Uncommon, 1),
        (typeof(SetupStrike), CardRarity.Common, 1),
        (typeof(ShrugItOff), CardRarity.Common, 1),
        (typeof(Spite), CardRarity.Uncommon, 0),
        (typeof(Stampede), CardRarity.Uncommon, 2),
        (typeof(Stoke), CardRarity.Rare, 1),
        (typeof(Stomp), CardRarity.Uncommon, 3),
        (typeof(StoneArmor), CardRarity.Uncommon, 1),
        (typeof(StrikeIronclad), CardRarity.Basic, 1),
        (typeof(SwordBoomerang), CardRarity.Common, 1),
        (typeof(Tank), CardRarity.Rare, 1),
        (typeof(Taunt), CardRarity.Common, 1),
        (typeof(TearAsunder), CardRarity.Rare, 2),
        (typeof(Thrash), CardRarity.Rare, 1),
        (typeof(Thunderclap), CardRarity.Common, 1),
        (typeof(Tremble), CardRarity.Common, 1),
        (typeof(TrueGrit), CardRarity.Common, 1),
        (typeof(TwinStrike), CardRarity.Common, 1),
        (typeof(Unmovable), CardRarity.Rare, 2),
        (typeof(Unrelenting), CardRarity.Uncommon, 2),
        (typeof(Uppercut), CardRarity.Uncommon, 2),
        (typeof(Vicious), CardRarity.Uncommon, 1),
        (typeof(Whirlwind), CardRarity.Uncommon, 0),
    ];

    private static readonly Type[] MultiplayerOnlyCards =
        [typeof(Blaze), typeof(DemonicShield), typeof(Midnight), typeof(Outrage), typeof(Tank)];

    public IroncladPoolTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CardPool_MatchesSourceOrderAndMetadata()
    {
        Ironclad ironclad = ModelDb.Character<Ironclad>();
        IReadOnlyList<CardModel> actual = ironclad.CardPool.AllCards;

        Assert.Equal(90, SourceCards.Length);
        Assert.Equal(SourceCards.Select(entry => entry.Type), actual.Select(card => card.GetType()));
        Assert.Equal(SourceCards.Select(entry => entry.Rarity), actual.Select(card => card.Rarity));
        Assert.Equal(SourceCards.Select(entry => entry.Cost), actual.Select(card => card.CanonicalEnergyCostValue));
        Assert.Equal(90, actual.Select(card => card.Id).Distinct().Count());
        Assert.Equal(MultiplayerOnlyCards,
            actual.Where(card => card.IsMultiplayerOnly).Select(card => card.GetType()));
        Assert.All(actual, card => Assert.True(card.IsUpgradable, card.GetType().Name));

        PlayerUnlockState allUnlocked = PlayerUnlockState.AllUnlocked();
        Assert.Equal(SourceCards.Select(entry => entry.Type),
            ironclad.CardPool.GetUnlockedCards(allUnlocked, isMultiplayer: true)
                .Select(card => card.GetType()));
        Assert.Equal(SourceCards.Select(entry => entry.Type)
                .Where(type => !MultiplayerOnlyCards.Contains(type)),
            ironclad.CardPool.GetUnlockedCards(allUnlocked, isMultiplayer: false)
                .Select(card => card.GetType()));

        // Upgrade metadata from Havoc, Juggling and DrumOfBattle's native OnUpgrade.
        Havoc havoc = (Havoc)ModelDb.Card<Havoc>().MutableClone();
        CardCmd.Upgrade(havoc);
        Assert.Equal(0, havoc.EnergyCost);
        Juggling juggling = (Juggling)ModelDb.Card<Juggling>().MutableClone();
        CardCmd.Upgrade(juggling);
        Assert.True(juggling.HasKeyword(CardKeyword.Innate));
        DrumOfBattle drum = (DrumOfBattle)ModelDb.Card<DrumOfBattle>().MutableClone();
        CardCmd.Upgrade(drum);
        Assert.Equal(3d, ((ICardChoiceBaseValueProvider)drum).CardChoiceBaseValues?.Energy);
    }

    [Theory]
    [InlineData(CardRarity.Common, typeof(Anger))]
    [InlineData(CardRarity.Uncommon, typeof(BattleTrance))]
    [InlineData(CardRarity.Rare, typeof(Aggression))]
    public void RewardTransformMerchant_StayInOwnerPool(CardRarity rarity, Type sourceType)
    {
        var run = new RunState($"ironclad-pools-{rarity}", new Overgrowth());
        Player ironclad = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run);
        run.AddPlayer(ironclad);
        HashSet<Type> sourcePool = SourceCards.Select(entry => entry.Type).ToHashSet();

        IReadOnlyList<CardModel> rewards = CardFactory.CreateForReward(
            ironclad, 3, ironclad.Character.CardPool, rarity);
        Assert.Equal(3, rewards.Count);
        Assert.All(rewards, card =>
        {
            Assert.Contains(card.GetType(), sourcePool);
            Assert.Equal(rarity, card.Rarity);
            Assert.False(card.IsMultiplayerOnly);
        });

        IReadOnlyList<CardModel> encounterRewards = CardFactory.CreateForReward(
            ironclad, 3, CardRarityOddsType.RegularEncounter);
        Assert.NotEmpty(encounterRewards);
        Assert.All(encounterRewards, card => Assert.Contains(card.GetType(), sourcePool));

        CardModel source = (CardModel)ModelDb.Get(sourceType).MutableClone();
        source.AssignOwner(ironclad);
        CardModel transformed = CardFactory.CreateRandomCardForTransform(
            source, isInCombat: false, new Rng(12345UL));
        Assert.Contains(transformed.GetType(), sourcePool);
        Assert.NotEqual(sourceType, transformed.GetType());
        Assert.Equal(ironclad, transformed.Owner);

        CardModel[] shopCards = MerchantInventory.Generate(ironclad).Cards.Take(5)
            .Select(entry => entry.Card).ToArray();
        Assert.Equal(5, shopCards.Length);
        Assert.All(shopCards, card =>
        {
            Assert.Contains(card.GetType(), sourcePool);
            Assert.False(card.IsMultiplayerOnly);
            Assert.Contains(card.Rarity, new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare });
        });

        foreach (CharacterModel otherCharacter in new CharacterModel[]
                 { ModelDb.Character<Silent>(), ModelDb.Character<Regent>() })
        {
            var otherRun = new RunState($"owner-pool-{otherCharacter.Id.Entry}-{rarity}", new Overgrowth());
            Player other = Player.CreateForNewRun(otherCharacter, otherRun);
            otherRun.AddPlayer(other);
            HashSet<Type> ownPool = otherCharacter.CardPool.AllCards
                .Select(card => card.GetType()).ToHashSet();
            Assert.Empty(ownPool.Intersect(sourcePool));

            IReadOnlyList<CardModel> otherRewards = CardFactory.CreateForReward(
                other, 3, otherCharacter.CardPool, rarity);
            Assert.NotEmpty(otherRewards);
            Assert.All(otherRewards, card => Assert.Contains(card.GetType(), ownPool));

            CardModel otherSource = (CardModel)otherCharacter.CardPool.AllCards
                .First(card => card.Rarity == rarity).MutableClone();
            otherSource.AssignOwner(other);
            CardModel otherTransform = CardFactory.CreateRandomCardForTransform(
                otherSource, isInCombat: false, new Rng(12345UL));
            Assert.Contains(otherTransform.GetType(), ownPool);

            CardModel[] otherShop = MerchantInventory.Generate(other).Cards.Take(5)
                .Select(entry => entry.Card).ToArray();
            Assert.Equal(5, otherShop.Length);
            Assert.All(otherShop, card => Assert.Contains(card.GetType(), ownPool));
        }
    }

    [Fact]
    public void RelicPotionPools_MatchSourceAndIsolation()
    {
        // Native IroncladRelicPool and Ironclad4Epoch; old pools are their native definitions.
        Ironclad ironclad = ModelDb.Character<Ironclad>();
        PlayerUnlockState allUnlocked = PlayerUnlockState.AllUnlocked();
        Assert.Equal(new[]
        {
            typeof(Brimstone), typeof(BurningBlood), typeof(CharonsAshes),
            typeof(DemonTongue), typeof(PaperPhrog), typeof(RedSkull),
            typeof(RuinedHelmet), typeof(SelfFormingClay),
        }, ironclad.RelicPool.AllRelics.Select(relic => relic.GetType()));
        Type[] ironcladPotions =
            [typeof(BloodPotion), typeof(SoldiersStew), typeof(Ashwater)];
        Assert.Equal(ironcladPotions,
            ironclad.PotionPool.AllPotions.Select(potion => potion.GetType()));
        Assert.Equal(ironcladPotions,
            ironclad.PotionPool.GetUnlockedPotions(allUnlocked).Select(potion => potion.GetType()));

        Silent silent = ModelDb.Character<Silent>();
        Assert.Equal(new[]
        {
            typeof(HelicalDart), typeof(NinjaScroll), typeof(PaperKrane),
            typeof(RingOfTheSnake), typeof(SneckoSkull), typeof(Tingsha),
            typeof(ToughBandages), typeof(TwistedFunnel),
        }, silent.RelicPool.AllRelics.Select(relic => relic.GetType()));
        Assert.Equal(new[] { typeof(PoisonPotion), typeof(GhostInAJar), typeof(CunningPotion) },
            silent.PotionPool.GetUnlockedPotions(allUnlocked).Select(potion => potion.GetType()));

        Regent regent = ModelDb.Character<Regent>();
        Assert.Equal(new[]
        {
            typeof(DivineRight), typeof(FencingManual), typeof(GalacticDust),
            typeof(LunarPastry), typeof(MiniRegent), typeof(OrangeDough),
            typeof(Regalite), typeof(VitruvianMinion),
        }, regent.RelicPool.AllRelics.Select(relic => relic.GetType()));
        Assert.Equal(new[] { typeof(StarPotion), typeof(CosmicConcoction), typeof(KingsCourage) },
            regent.PotionPool.GetUnlockedPotions(allUnlocked).Select(potion => potion.GetType()));
    }
}
