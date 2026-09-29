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
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class NecrobinderPoolTests : IDisposable
{
    // Transcribed from v0.111.0 NecrobinderCardPool.GenerateAllCards and each card constructor.
    // These expectations deliberately do not read NecrobinderCardPool's production type table.
    private static readonly (Type Type, CardRarity Rarity, int Cost)[] SourceCards =
    [
        (typeof(Afterlife), CardRarity.Common, 1),
        (typeof(BansheesCry), CardRarity.Rare, 9),
        (typeof(BlightStrike), CardRarity.Common, 1),
        (typeof(Bodyguard), CardRarity.Basic, 1),
        (typeof(BoneShards), CardRarity.Uncommon, 1),
        (typeof(BorrowedTime), CardRarity.Uncommon, 1),
        (typeof(Bury), CardRarity.Uncommon, 4),
        (typeof(Cacophony), CardRarity.Rare, 2),
        (typeof(Calcify), CardRarity.Uncommon, 1),
        (typeof(CallOfTheVoid), CardRarity.Rare, 1),
        (typeof(CaptureSpirit), CardRarity.Uncommon, 1),
        (typeof(Cleanse), CardRarity.Uncommon, 1),
        (typeof(Countdown), CardRarity.Uncommon, 1),
        (typeof(DanseMacabre), CardRarity.Uncommon, 1),
        (typeof(DeathMarch), CardRarity.Uncommon, 1),
        (typeof(Deathbringer), CardRarity.Uncommon, 2),
        (typeof(DeathsDoor), CardRarity.Uncommon, 1),
        (typeof(Debilitate), CardRarity.Uncommon, 1),
        (typeof(DefendNecrobinder), CardRarity.Basic, 1),
        (typeof(Defile), CardRarity.Common, 1),
        (typeof(Defy), CardRarity.Common, 1),
        (typeof(Delay), CardRarity.Uncommon, 2),
        (typeof(Demesne), CardRarity.Rare, 3),
        (typeof(DevourLife), CardRarity.Rare, 1),
        (typeof(Dirge), CardRarity.Uncommon, 0),
        (typeof(DrainPower), CardRarity.Common, 1),
        (typeof(Dredge), CardRarity.Uncommon, 1),
        (typeof(Eidolon), CardRarity.Rare, 2),
        (typeof(EndOfDays), CardRarity.Rare, 3),
        (typeof(EnfeeblingTouch), CardRarity.Uncommon, 1),
        (typeof(Eradicate), CardRarity.Rare, 0),
        (typeof(Fear), CardRarity.Common, 1),
        (typeof(Fetch), CardRarity.Uncommon, 0),
        (typeof(Flatten), CardRarity.Common, 2),
        (typeof(ForbiddenGrimoire), CardRarity.Ancient, 2),
        (typeof(Friendship), CardRarity.Uncommon, 1),
        (typeof(GlimpseBeyond), CardRarity.Rare, 1),
        (typeof(GraveWarden), CardRarity.Common, 1),
        (typeof(Graveblast), CardRarity.Common, 1),
        (typeof(Hang), CardRarity.Rare, 1),
        (typeof(Haunt), CardRarity.Uncommon, 1),
        (typeof(HighFive), CardRarity.Uncommon, 2),
        (typeof(Invoke), CardRarity.Common, 1),
        (typeof(LegionOfBone), CardRarity.Uncommon, 2),
        (typeof(Lethality), CardRarity.Uncommon, 1),
        (typeof(Melancholy), CardRarity.Uncommon, 3),
        (typeof(Misery), CardRarity.Rare, 0),
        (typeof(NecroMastery), CardRarity.Rare, 2),
        (typeof(NegativePulse), CardRarity.Common, 1),
        (typeof(Neurosurge), CardRarity.Rare, 0),
        (typeof(NoEscape), CardRarity.Uncommon, 1),
        (typeof(Oblivion), CardRarity.Rare, 0),
        (typeof(Pagestorm), CardRarity.Uncommon, 1),
        (typeof(Parse), CardRarity.Uncommon, 1),
        (typeof(Poke), CardRarity.Common, 0),
        (typeof(Protector), CardRarity.Ancient, 1),
        (typeof(PullAggro), CardRarity.Common, 2),
        (typeof(PullFromBelow), CardRarity.Uncommon, 1),
        (typeof(Putrefy), CardRarity.Uncommon, 1),
        (typeof(Rattle), CardRarity.Uncommon, 1),
        (typeof(Reanimate), CardRarity.Rare, 3),
        (typeof(Reap), CardRarity.Common, 3),
        (typeof(ReaperForm), CardRarity.Rare, 3),
        (typeof(Reave), CardRarity.Common, 1),
        (typeof(RightHandHand), CardRarity.Uncommon, 0),
        (typeof(Sacrifice), CardRarity.Rare, 1),
        (typeof(Scourge), CardRarity.Common, 1),
        (typeof(SculptingStrike), CardRarity.Common, 1),
        (typeof(Seance), CardRarity.Rare, 1),
        (typeof(SentryMode), CardRarity.Rare, 2),
        (typeof(Severance), CardRarity.Uncommon, 2),
        (typeof(SharedFate), CardRarity.Rare, 0),
        (typeof(Shroud), CardRarity.Uncommon, 1),
        (typeof(SicEm), CardRarity.Uncommon, 1),
        (typeof(SleightOfFlesh), CardRarity.Uncommon, 2),
        (typeof(Snap), CardRarity.Common, 1),
        (typeof(SoulStorm), CardRarity.Rare, 1),
        (typeof(Soulbound), CardRarity.Uncommon, 1),
        (typeof(Sow), CardRarity.Common, 1),
        (typeof(SpiritOfAsh), CardRarity.Rare, 1),
        (typeof(Spur), CardRarity.Uncommon, 1),
        (typeof(Squeeze), CardRarity.Rare, 3),
        (typeof(StrikeNecrobinder), CardRarity.Basic, 1),
        (typeof(TheScythe), CardRarity.Rare, 2),
        (typeof(TimesUp), CardRarity.Rare, 2),
        (typeof(Transfigure), CardRarity.Rare, 1),
        (typeof(Undeath), CardRarity.Rare, 0),
        (typeof(Underworld), CardRarity.Uncommon, 2),
        (typeof(Unleash), CardRarity.Basic, 1),
        (typeof(Veilpiercer), CardRarity.Uncommon, 1),
        (typeof(Wisp), CardRarity.Common, 0),
    ];

    private static readonly Type[] MultiplayerOnlyCards =
    [
        typeof(Cacophony), typeof(GlimpseBeyond), typeof(LegionOfBone),
        typeof(Soulbound), typeof(Underworld),
    ];

    // NecrobinderCardPool.FilterThroughEpochs, v0.111.0.
    private static readonly (string Epoch, Type[] Cards)[] EpochGatedCards =
    [
        ("NECROBINDER2_EPOCH", [typeof(Scourge), typeof(Oblivion), typeof(Countdown)]),
        ("NECROBINDER5_EPOCH", [typeof(Afterlife), typeof(Sacrifice), typeof(Calcify)]),
        ("NECROBINDER7_EPOCH", [typeof(SculptingStrike), typeof(Veilpiercer), typeof(BansheesCry)]),
    ];

    public NecrobinderPoolTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CardPool_MatchesSourceOrderAndMetadata()
    {
        Necrobinder necrobinder = ModelDb.Character<Necrobinder>();
        IReadOnlyList<CardModel> actual = necrobinder.CardPool.AllCards;

        Assert.Equal(91, SourceCards.Length);
        Assert.Equal(SourceCards.Select(entry => entry.Type), actual.Select(card => card.GetType()));
        Assert.Equal(SourceCards.Select(entry => entry.Rarity), actual.Select(card => card.Rarity));
        Assert.Equal(SourceCards.Select(entry => entry.Cost), actual.Select(card => card.CanonicalEnergyCostValue));
        Assert.Equal(91, actual.Select(card => card.Id).Distinct().Count());
        Assert.Equal(MultiplayerOnlyCards,
            actual.Where(card => card.IsMultiplayerOnly).Select(card => card.GetType()));
        Assert.True(ModelDb.Card<Dirge>().CostsXEnergy);
        // ArchaicTooth maps Unleash to Protector, which also keeps DustyTome from offering it.
        Assert.Contains(ModelDb.Card<Protector>(), ArchaicTooth.TranscendenceCards);

        PlayerUnlockState allUnlocked = PlayerUnlockState.AllUnlocked();
        Assert.Equal(SourceCards.Select(entry => entry.Type),
            necrobinder.CardPool.GetUnlockedCards(allUnlocked, isMultiplayer: true)
                .Select(card => card.GetType()));
        Assert.Equal(SourceCards.Select(entry => entry.Type)
                .Where(type => !MultiplayerOnlyCards.Contains(type)),
            necrobinder.CardPool.GetUnlockedCards(allUnlocked, isMultiplayer: false)
                .Select(card => card.GetType()));

        // Each epoch hides exactly its three cards; revealing it restores them.
        foreach ((string epoch, Type[] gated) in EpochGatedCards)
        {
            string[] others = EpochGatedCards.Select(entry => entry.Epoch).Where(id => id != epoch).ToArray();
            var partial = new PlayerUnlockState([], others);
            Assert.Equal(SourceCards.Select(entry => entry.Type).Where(type => !gated.Contains(type)),
                necrobinder.CardPool.GetUnlockedCards(partial, isMultiplayer: true)
                    .Select(card => card.GetType()));
        }

        // Upgrade metadata from Demesne, CallOfTheVoid and Transfigure's native OnUpgrade.
        Demesne demesne = (Demesne)ModelDb.Card<Demesne>().MutableClone();
        CardCmd.Upgrade(demesne);
        Assert.Equal(2, demesne.EnergyCost);
        CallOfTheVoid callOfTheVoid = (CallOfTheVoid)ModelDb.Card<CallOfTheVoid>().MutableClone();
        CardCmd.Upgrade(callOfTheVoid);
        Assert.True(callOfTheVoid.HasKeyword(CardKeyword.Innate));
        Transfigure transfigure = (Transfigure)ModelDb.Card<Transfigure>().MutableClone();
        Assert.True(transfigure.HasKeyword(CardKeyword.Exhaust));
        CardCmd.Upgrade(transfigure);
        Assert.False(transfigure.HasKeyword(CardKeyword.Exhaust));
    }

    [Theory]
    [InlineData(CardRarity.Common, typeof(Afterlife))]
    [InlineData(CardRarity.Uncommon, typeof(BoneShards))]
    [InlineData(CardRarity.Rare, typeof(BansheesCry))]
    public void RewardTransformMerchant_StayInOwnerPool(CardRarity rarity, Type sourceType)
    {
        var run = new RunState($"necrobinder-pools-{rarity}", new Overgrowth());
        Player necrobinder = Player.CreateForNewRun(ModelDb.Character<Necrobinder>(), run);
        run.AddPlayer(necrobinder);
        HashSet<Type> sourcePool = SourceCards.Select(entry => entry.Type).ToHashSet();

        IReadOnlyList<CardModel> rewards = CardFactory.CreateForReward(
            necrobinder, 3, necrobinder.Character.CardPool, rarity);
        Assert.Equal(3, rewards.Count);
        Assert.All(rewards, card =>
        {
            Assert.Contains(card.GetType(), sourcePool);
            Assert.Equal(rarity, card.Rarity);
            Assert.False(card.IsMultiplayerOnly);
        });

        IReadOnlyList<CardModel> encounterRewards = CardFactory.CreateForReward(
            necrobinder, 3, CardRarityOddsType.RegularEncounter);
        Assert.NotEmpty(encounterRewards);
        Assert.All(encounterRewards, card => Assert.Contains(card.GetType(), sourcePool));

        CardModel source = (CardModel)ModelDb.Get(sourceType).MutableClone();
        source.AssignOwner(necrobinder);
        CardModel transformed = CardFactory.CreateRandomCardForTransform(
            source, isInCombat: false, new Rng(12345UL));
        Assert.Contains(transformed.GetType(), sourcePool);
        Assert.NotEqual(sourceType, transformed.GetType());
        Assert.Equal(necrobinder, transformed.Owner);

        CardModel[] shopCards = MerchantInventory.Generate(necrobinder).Cards.Take(5)
            .Select(entry => entry.Card).ToArray();
        Assert.Equal(5, shopCards.Length);
        Assert.All(shopCards, card =>
        {
            Assert.Contains(card.GetType(), sourcePool);
            Assert.False(card.IsMultiplayerOnly);
            Assert.Contains(card.Rarity, new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare });
        });

        foreach (CharacterModel otherCharacter in new CharacterModel[]
                 { ModelDb.Character<Silent>(), ModelDb.Character<Regent>(), ModelDb.Character<Ironclad>() })
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
        // Native NecrobinderRelicPool and Necrobinder4Epoch potions.
        Necrobinder necrobinder = ModelDb.Character<Necrobinder>();
        PlayerUnlockState allUnlocked = PlayerUnlockState.AllUnlocked();
        Type[] necrobinderRelics =
        [
            typeof(BigHat), typeof(BoneFlute), typeof(BookRepairKnife), typeof(Bookmark),
            typeof(BoundPhylactery), typeof(FuneraryMask), typeof(IvoryTile), typeof(UndyingSigil),
        ];
        Assert.Equal(necrobinderRelics, necrobinder.RelicPool.AllRelics.Select(relic => relic.GetType()));
        Type[] necrobinderPotions = [typeof(PotionOfDoom), typeof(PotOfGhouls), typeof(BoneBrew)];
        Assert.Equal(necrobinderPotions,
            necrobinder.PotionPool.AllPotions.Select(potion => potion.GetType()));
        Assert.Equal(necrobinderPotions,
            necrobinder.PotionPool.GetUnlockedPotions(allUnlocked).Select(potion => potion.GetType()));
        Assert.Empty(necrobinder.PotionPool.GetUnlockedPotions(new PlayerUnlockState([], [])));
        Assert.Equal(necrobinderPotions, necrobinder.PotionPool.GetUnlockedPotions(
            new PlayerUnlockState([], [NecrobinderPotionPool.Necrobinder4EpochId])).Select(potion => potion.GetType()));

        foreach (CharacterModel other in new CharacterModel[]
                 { ModelDb.Character<Silent>(), ModelDb.Character<Regent>(), ModelDb.Character<Ironclad>() })
        {
            Assert.Equal(8, other.RelicPool.AllRelics.Count);
            Assert.Empty(other.RelicPool.AllRelics.Select(relic => relic.GetType()).Intersect(necrobinderRelics));
            Assert.Equal(3, other.PotionPool.GetUnlockedPotions(allUnlocked).Count());
            Assert.Empty(other.PotionPool.GetUnlockedPotions(allUnlocked)
                .Select(potion => potion.GetType()).Intersect(necrobinderPotions));
        }
    }
}
