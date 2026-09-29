using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Potions;

[Collection("ModelDb")]
public sealed class CommonPotionBatch2Tests : IDisposable
{
    private static readonly string[] PotionNames =
    {
        "PowerPotion",
        "SkillPotion",
        "SpeedPotion",
        "StarPotion",
        "SwiftPotion",
        "VulnerablePotion",
        "WeakPotion",
    };

    public CommonPotionBatch2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(PotionBatch2TestCharacter),
            typeof(WanderingGrunt),
            typeof(TemporaryDexterityPower), typeof(DexterityPower),
            typeof(VulnerablePower),
            typeof(WeakPower),
            typeof(PotionPowerCandidateA),
            typeof(PotionPowerCandidateB),
            typeof(PotionPowerCandidateC),
            typeof(PotionSkillCandidateA),
            typeof(PotionSkillCandidateB),
            typeof(PotionSkillCandidateC),
            typeof(PotionColorlessPowerCandidate),
            typeof(PotionForbiddenPowerCandidate),
            typeof(PotionColorlessSkillCandidate),
            typeof(PotionForbiddenSkillCandidate),
        }.Concat(ResolveTask6Types()));
    }

    public void Dispose()
    {
        PotionBatch2GenerationProbe.Reset();
        ModelDb.ResetForTests();
    }

    public static TheoryData<string, TargetType> MetadataCases => new()
    {
        { "PowerPotion", TargetType.AnyPlayer },
        { "SkillPotion", TargetType.AnyPlayer },
        { "SpeedPotion", TargetType.AnyPlayer },
        { "StarPotion", TargetType.AnyPlayer },
        { "SwiftPotion", TargetType.AnyPlayer },
        { "VulnerablePotion", TargetType.AnyEnemy },
        { "WeakPotion", TargetType.AnyEnemy },
    };

    [Theory]
    [MemberData(nameof(MetadataCases))]
    public void Metadata_MatchesCommonCombatOnlyTable(string potionName, TargetType targetType)
    {
        PotionModel potion = ModelDb.GetById<PotionModel>(ModelDb.GetId(RequireTask6Type(potionName)));

        Assert.Equal(PotionRarity.Common, potion.Rarity);
        Assert.Equal(PotionUsage.CombatOnly, potion.Usage);
        Assert.Equal(targetType, potion.TargetType);
    }

    [Theory]
    [InlineData("PowerPotion", CardType.Power)]
    [InlineData("SkillPotion", CardType.Skill)]
    public async Task CardGenerationPotions_GenerateThreeDistinctEligibleChoices_AndSelectFirstFreeIntoHand(
        string potionName,
        CardType requiredType)
    {
        GenerationResult first = await UseGenerationPotionAsync(potionName, $"{potionName}-deterministic");
        GenerationResult second = await UseGenerationPotionAsync(potionName, $"{potionName}-deterministic");

        AssertGenerationContract(first, requiredType);
        AssertGenerationDeterminism(first, second);
    }

    [Fact]
    public async Task SpeedPotion_AppliesFiveTemporaryDexterity_UntilPlayerSideEnds()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("speed-potion");
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await UsePotionAsync("SpeedPotion", player, player.Creature);

        PowerModel speed = Assert.Single(player.Creature.Powers.OfType<SpeedPotionPower>());
        Assert.Equal(2, player.Creature.Powers.Count);
        Assert.Equal(5, player.Creature.GetPower<DexterityPower>()!.Amount);
        Assert.Same(player.Creature, speed.Applier);
        Assert.Equal("SpeedPotionPower", speed.GetType().Name);
        Assert.IsAssignableFrom<TemporaryDexterityPower>(speed);
        Assert.Equal(5m, speed.Amount);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, new[] { enemy });
        Assert.Same(speed, Assert.Single(player.Creature.Powers.OfType<SpeedPotionPower>()));

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Player, new[] { player.Creature });
        Assert.Empty(player.Creature.Powers);
    }

    [Fact]
    public async Task StarPotion_GainsExactlyThreeStars()
    {
        (Player player, _) = await CreateCombatAsync("star-potion");
        Assert.Equal(0, player.PlayerCombatState!.Stars);

        await UsePotionAsync("StarPotion", player, player.Creature);

        Assert.Equal(3, player.PlayerCombatState.Stars);
    }

    [Fact]
    public async Task SwiftPotion_DrawsExactlyThreeCards()
    {
        (Player player, _) = await CreateCombatAsync("swift-potion");
        AddCopiesToDrawPile<PotionSkillCandidateA>(player, 4);
        Assert.Empty(player.PlayerCombatState!.Hand.Cards);

        await UsePotionAsync("SwiftPotion", player, player.Creature);

        Assert.Equal(3, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Single(player.PlayerCombatState.DrawPile.Cards);
    }

    [Theory]
    [InlineData("VulnerablePotion", typeof(VulnerablePower))]
    [InlineData("WeakPotion", typeof(WeakPower))]
    public async Task DebuffPotions_ApplyExactlyThreeToTargetEnemy_ThenDecayAtEnemySideEnd(
        string potionName,
        Type powerType)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"{potionName}-exact");
        Creature target = room.Engine.State.HittableEnemies.Single();

        await UsePotionAsync(potionName, player, target);

        PowerModel power = Assert.Single(target.Powers, candidate => candidate.GetType() == powerType);
        Assert.Equal(3m, power.Amount);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, new[] { target });
        Assert.Equal(2m, power.Amount);
    }

    private static void AssertGenerationContract(GenerationResult result, CardType requiredType)
    {
        Assert.Equal(3, result.GeneratedTypes.Count);
        Assert.Equal(3, result.GeneratedTypes.Distinct().Count());
        Assert.All(result.GeneratedTypes, generatedType =>
        {
            CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(generatedType));
            Assert.False(canonical.IsColorless);
            Assert.Equal(requiredType, canonical.Type);
            Assert.True(canonical.CanBeGeneratedInCombat);
        });
        Assert.Equal(result.GeneratedTypes[0], result.SelectedType);
        // Three eligible cards: native full-pool Fisher–Yates consumes N-1 draws.
        Assert.Equal(2, result.GenerationCounter);
        Assert.Equal(0, result.SelectionCounter);
        Assert.Equal(PileType.Hand, result.SelectedPile);
        Assert.True(result.SelectedTemporaryFreeThisTurn);
        Assert.Equal(0, result.SelectedEnergyCost);
    }

    private static void AssertGenerationDeterminism(GenerationResult first, GenerationResult second)
    {
        Assert.True(first.GeneratedTypes.SequenceEqual(second.GeneratedTypes));
        Assert.Equal(first.SelectedType, second.SelectedType);
        Assert.Equal(first.GenerationCounter, second.GenerationCounter);
        Assert.Equal(first.SelectionCounter, second.SelectionCounter);
        Assert.Equal(first.SelectedPile, second.SelectedPile);
        Assert.Equal(first.SelectedTemporaryFreeThisTurn, second.SelectedTemporaryFreeThisTurn);
        Assert.Equal(first.SelectedEnergyCost, second.SelectedEnergyCost);
    }

    private static async Task<GenerationResult> UseGenerationPotionAsync(string potionName, string seed)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync(seed);
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        PotionBatch2GenerationProbe.Reset();

        await UsePotionAsync(potionName, player, player.Creature);

        CardModel selected = Assert.Single(player.PlayerCombatState!.Hand.Cards);
        return new GenerationResult(
            PotionBatch2GenerationProbe.ClonedTypes.ToArray(),
            selected.GetType(),
            room.Engine.State.RunState.Rng.CombatCardGeneration.Counter,
            room.Engine.State.RunState.Rng.CombatCardSelection.Counter,
            selected.Pile!.Type,
            selected.TemporaryFreeThisTurn,
            selected.EnergyCost);
    }

    private static async Task UsePotionAsync(string potionName, Player player, Creature target)
    {
        Type potionType = RequireTask6Type(potionName);
        PotionModel canonical = ModelDb.GetById<PotionModel>(ModelDb.GetId(potionType));
        PotionModel potion = player.AddPotionInternal(canonical);
        await PotionCmd.Use(potion, player, target);
    }

    private static void AddCopiesToDrawPile<TCard>(Player player, int count)
        where TCard : CardModel
    {
        for (int i = 0; i < count; i++)
        {
            var card = (TCard)ModelDb.Card<TCard>().MutableClone();
            card.AssignOwner(player);
            CardPileCmd.Add(card, PileType.Draw);
        }
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<PotionBatch2TestCharacter>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }

    private static IEnumerable<Type> ResolveTask6Types() =>
        PotionNames
            .Append("SpeedPotionPower")
            .Select(RequireTask6Type);

    private static Type RequireTask6Type(string name) =>
        typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.{(name.EndsWith("Power") ? "Powers" : "Potions")}.{name}")
        ?? throw new Xunit.Sdk.XunitException($"Task 6 model {name} is not implemented.");

    private sealed record GenerationResult(
        IReadOnlyList<Type> GeneratedTypes,
        Type SelectedType,
        int GenerationCounter,
        int SelectionCounter,
        PileType SelectedPile,
        bool SelectedTemporaryFreeThisTurn,
        int SelectedEnergyCost);
}

file static class PotionBatch2GenerationProbe
{
    private static readonly List<Type> Cloned = new();

    public static IReadOnlyList<Type> ClonedTypes => Cloned;

    public static void Record(Type type) => Cloned.Add(type);

    public static void Reset() => Cloned.Clear();
}

file sealed class PotionBatch2TestCharacter : CharacterModel
{
    public override int StartingHp => 75;

    public override int StartingGold => 99;

    // 偏离 #319：药水/卡牌生成改走 Character.CardPool 之后，测试替身角色必须提供卡池，
    // 否则落到 EmptyCardPool 会生成不出任何候选。
    private PotionBatch2TestCardPool? _cardPool;
    public override CardPoolModel CardPool => _cardPool ??= new PotionBatch2TestCardPool();
}

file abstract class PotionBatch2GenerationCandidate : CardModel
{
    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        PotionBatch2GenerationProbe.Record(GetType());
    }
}

file sealed class PotionColorlessPowerCandidate : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Power;
    public override bool IsColorless => true;
}

file sealed class PotionForbiddenPowerCandidate : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Power;
    public override bool CanBeGeneratedInCombat => false;
}

file sealed class PotionColorlessSkillCandidate : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Skill;
    public override bool IsColorless => true;
}

file sealed class PotionForbiddenSkillCandidate : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Skill;
    public override bool CanBeGeneratedInCombat => false;
}

file sealed class PotionPowerCandidateA : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Power;
}

file sealed class PotionPowerCandidateB : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Power;
}

file sealed class PotionPowerCandidateC : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Power;
}

file sealed class PotionSkillCandidateA : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Skill;
}

file sealed class PotionSkillCandidateB : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Skill;
}

file sealed class PotionSkillCandidateC : PotionBatch2GenerationCandidate
{
    public override CardType Type => CardType.Skill;
}

file sealed class PotionBatch2TestCardPool : CardPoolModel
{
    public override IReadOnlyList<CardModel> AllCards =>
    [
        ModelDb.Card<PotionPowerCandidateA>(),
        ModelDb.Card<PotionPowerCandidateB>(),
        ModelDb.Card<PotionPowerCandidateC>(),
        ModelDb.Card<PotionSkillCandidateA>(),
        ModelDb.Card<PotionSkillCandidateB>(),
        ModelDb.Card<PotionSkillCandidateC>(),
    ];
}
