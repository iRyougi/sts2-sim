using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class BlankDeadCardBatchTests : IDisposable
{
    public BlankDeadCardBatchTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<Type, CardType, CardRarity, CardKeyword[], bool, bool> Cards => new()
    {
        { typeof(Clumsy), CardType.Curse, CardRarity.Curse, new[] { CardKeyword.Unplayable, CardKeyword.Ethereal }, true, true },
        { typeof(CurseOfTheBell), CardType.Curse, CardRarity.Curse, new[] { CardKeyword.Eternal, CardKeyword.Unplayable }, false, true },
        { typeof(Folly), CardType.Curse, CardRarity.Curse, new[] { CardKeyword.Unplayable, CardKeyword.Eternal, CardKeyword.Innate, CardKeyword.Ethereal }, false, true },
        { typeof(Writhe), CardType.Curse, CardRarity.Curse, new[] { CardKeyword.Innate, CardKeyword.Unplayable }, true, true },
        { typeof(Soot), CardType.Status, CardRarity.Status, new[] { CardKeyword.Unplayable }, true, false },
    };

    [Theory]
    [MemberData(nameof(Cards))]
    public void MetadataAndGenerationGates_MatchAuthoritativeSource(
        Type cardType,
        CardType expectedType,
        CardRarity expectedRarity,
        CardKeyword[] expectedKeywords,
        bool expectedModifierGeneration,
        bool expectedCombatGeneration)
    {
        CardModel card = (CardModel)ModelDb.Get(cardType);

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(expectedType, card.Type);
        Assert.Equal(expectedRarity, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.Equal(expectedKeywords, card.Keywords);
        Assert.Equal(expectedModifierGeneration, card.CanBeGeneratedByModifiers);
        Assert.Equal(expectedCombatGeneration, card.CanBeGeneratedInCombat);
    }

    [Fact]
    public void CombatGenerationPool_ExcludesSootButKeepsCombatEligibleDeadCards()
    {
        CardModel[] candidates =
        {
            ModelDb.Card<Clumsy>(),
            ModelDb.Card<CurseOfTheBell>(),
            ModelDb.Card<Folly>(),
            ModelDb.Card<Writhe>(),
            ModelDb.Card<Soot>(),
        };

        CardModel[] filtered = CardPoolFilters.ForCombatGeneration(candidates).ToArray();

        Assert.Equal(candidates[..4], filtered);
        Assert.DoesNotContain(filtered, card => card is Soot);
    }


    [Fact]
    public async Task VexingPuzzlebox_ProductionPathExcludesSootWhenUnfilteredPoolWouldSelectIt()
    {
        // Find a deterministic witness instead of pinning the assertion to the reflection-sorted
        // registry size: adding unrelated real cards legitimately changes the selected fixed seed.
        (RunState runState, Player player) = Enumerable.Range(0, 10_000)
            .Select(index => CreateRun($"blank-soot-witness-{index}"))
            .First(candidate => candidate.RunState.Rng.CombatCardGeneration.CloneExact()
                .NextItem(ModelDb.All<CardModel>().Where(card => !card.IsColorless)) is Soot);
        await RelicCmd.Obtain(ModelDb.Relic<VexingPuzzlebox>(), player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

        await room.Enter(runState);

        CardModel generated = Assert.Single(
            player.PlayerCombatState!.Hand.Cards,
            card => card.TemporaryFreeThisTurn);
        Assert.IsNotType<Soot>(generated);
    }

    [Fact]
    public async Task VexingPuzzlebox_ProductionPathGeneratesFromTheCharacterCardPool()
    {
        (RunState runState, Player player) = CreateRun("wave4-blank-105");

        // 偏离 #303：权威走 Owner.Character.CardPool + CardFactory.GetDistinctForCombat。
        // 此前实现（和本测试的复算）都从扁平的"全部非无色卡"里抽，静默猎手落地后
        // 那个池会混进他系角色的卡；这里改锁"生成的卡必属本角色卡池"。
        HashSet<ModelId> characterPoolIds = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, isMultiplayer: false)
            .Select(card => card.Id)
            .ToHashSet();
        Assert.NotEmpty(characterPoolIds);
        await RelicCmd.Obtain(ModelDb.Relic<VexingPuzzlebox>(), player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

        await room.Enter(runState);

        CardModel generated = Assert.Single(
            player.PlayerCombatState!.Hand.Cards,
            card => card.TemporaryFreeThisTurn);
        Assert.Contains(generated.Id, characterPoolIds);
        Assert.Same(player, generated.Owner);
    }

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
