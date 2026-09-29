using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Potions;

[Collection("ModelDb")]
public sealed class CommonPotionBatch1Tests : IDisposable
{
    public CommonPotionBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(PotionTestCharacter),
            typeof(WanderingGrunt),
            typeof(AttackPotion),
            typeof(BlockPotion),
            typeof(ColorlessPotion),
            typeof(DexterityPotion),
            typeof(EnergyPotion),
            typeof(ExplosiveAmpoule),
            typeof(FirePotion),
            typeof(FlexPotion),
            typeof(DexterityPower),
            typeof(FlexPotionPower),
            typeof(StrengthPower),
            typeof(VulnerablePower),
            typeof(PotionAttackCandidateA),
            typeof(PotionAttackCandidateB),
            typeof(PotionAttackCandidateC),
            typeof(PotionColorlessCandidateA),
            typeof(PotionColorlessCandidateB),
            typeof(PotionColorlessCandidateC),
        });
    }

    public void Dispose()
    {
        PotionGenerationProbe.Reset();
        ModelDb.ResetForTests();
    }

    public static TheoryData<Type, TargetType> MetadataCases => new()
    {
        { typeof(AttackPotion), TargetType.AnyPlayer },
        { typeof(BlockPotion), TargetType.AnyPlayer },
        { typeof(ColorlessPotion), TargetType.AnyPlayer },
        { typeof(DexterityPotion), TargetType.AnyPlayer },
        { typeof(EnergyPotion), TargetType.AnyPlayer },
        { typeof(ExplosiveAmpoule), TargetType.AllEnemies },
        { typeof(FirePotion), TargetType.AnyEnemy },
        { typeof(FlexPotion), TargetType.AnyPlayer },
    };

    [Theory]
    [MemberData(nameof(MetadataCases))]
    public void Metadata_MatchesCommonCombatOnlyTable(Type potionType, TargetType targetType)
    {
        PotionModel potion = ModelDb.GetById<PotionModel>(ModelDb.GetId(potionType));

        Assert.Equal(PotionRarity.Common, potion.Rarity);
        Assert.Equal(PotionUsage.CombatOnly, potion.Usage);
        Assert.Equal(targetType, potion.TargetType);
    }

    public static TheoryData<Type, string, decimal> DirectEffectCases => new()
    {
        { typeof(BlockPotion), "block", 12m },
        { typeof(DexterityPotion), "dexterity", 2m },
        { typeof(EnergyPotion), "energy", 2m },
        { typeof(ExplosiveAmpoule), "all-enemy-damage", 10m },
        { typeof(FirePotion), "single-enemy-damage", 20m },
        { typeof(FlexPotion), "flex", 5m },
    };

    [Theory]
    [MemberData(nameof(DirectEffectCases))]
    public async Task DirectEffects_UseExactUnpoweredValues(
        Type potionType,
        string effect,
        decimal expected)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"direct-{effect}");
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        switch (effect)
        {
            case "block":
                await PowerCmd.Apply<DexterityPower>(
                    room.Engine.State,
                    player.Creature,
                    50m,
                    player.Creature,
                    null);
                await UsePotionAsync(potionType, player, player.Creature);
                Assert.Equal(expected, player.Creature.Block);
                break;
            case "dexterity":
                await UsePotionAsync(potionType, player, player.Creature);
                Assert.Equal(expected, Assert.Single(player.Creature.Powers.OfType<DexterityPower>()).Amount);
                break;
            case "energy":
                player.PlayerCombatState!.Energy = 0;
                await UsePotionAsync(potionType, player, player.Creature);
                Assert.Equal(expected, player.PlayerCombatState.Energy);
                break;
            case "all-enemy-damage":
                await AddDamageModifiersAsync(room, player, enemy);
                int allEnemyHpBefore = enemy.CurrentHp;
                await UsePotionAsync(potionType, player, target: null);
                Assert.Equal(expected, allEnemyHpBefore - enemy.CurrentHp);
                break;
            case "single-enemy-damage":
                await AddDamageModifiersAsync(room, player, enemy);
                int enemyHpBefore = enemy.CurrentHp;
                await UsePotionAsync(potionType, player, enemy);
                Assert.Equal(expected, enemyHpBefore - enemy.CurrentHp);
                break;
            case "flex":
                await UsePotionAsync(potionType, player, player.Creature);
                FlexPotionPower flex = Assert.Single(player.Creature.Powers.OfType<FlexPotionPower>());
                Assert.IsAssignableFrom<TemporaryStrengthPower>(flex);
                Assert.Equal(expected, flex.Amount);
                Assert.Equal(expected, player.Creature.GetPower<StrengthPower>()!.Amount);
                Assert.Same(player.Creature, flex.Applier);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unknown direct potion effect.");
        }
    }

    [Fact]
    public async Task ExplosiveAmpoule_IgnoresPassedTarget_AndHitsOnlyHittableEnemies()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("ampoule-hittable-enemies");
        Creature firstLiving = room.Engine.State.HittableEnemies.Single();
        Creature secondLiving = room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            Sts2Sim.Core.Combat.CombatSide.Enemy);
        Creature deadEnemy = room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            Sts2Sim.Core.Combat.CombatSide.Enemy);
        deadEnemy.LoseHpInternal(deadEnemy.CurrentHp, ValueProp.Unpowered);
        int firstHpBefore = firstLiving.CurrentHp;
        int secondHpBefore = secondLiving.CurrentHp;

        var potion = (ExplosiveAmpoule)ModelDb.Potion<ExplosiveAmpoule>().MutableClone();
        potion.AssignOwner(player);
        await potion.UseInternal(player.Creature);

        Assert.Equal(firstHpBefore - 10, firstLiving.CurrentHp);
        Assert.Equal(secondHpBefore - 10, secondLiving.CurrentHp);
        Assert.Equal(0, deadEnemy.CurrentHp);
    }

    [Fact]
    public async Task AttackPotion_GeneratesThreeDistinctCharacterAttacks_AndSelectsFirstFreeIntoHand()
    {
        GenerationResult first = await UseGenerationPotionAsync<AttackPotion>("attack-potion-deterministic");
        GenerationResult second = await UseGenerationPotionAsync<AttackPotion>("attack-potion-deterministic");

        AssertGenerationContract(first, isColorless: false, requiredType: CardType.Attack);
        AssertGenerationDeterminism(first, second);
    }

    [Fact]
    public async Task ColorlessPotion_GeneratesThreeDistinctColorlessCards_AndSelectsFirstFreeIntoHand()
    {
        GenerationResult first = await UseGenerationPotionAsync<ColorlessPotion>("colorless-potion-deterministic");
        GenerationResult second = await UseGenerationPotionAsync<ColorlessPotion>("colorless-potion-deterministic");

        AssertGenerationContract(first, isColorless: true, requiredType: null);
        AssertGenerationDeterminism(first, second);
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

    private static void AssertGenerationContract(
        GenerationResult result,
        bool isColorless,
        CardType? requiredType)
    {
        Assert.Equal(3, result.GeneratedTypes.Count);
        Assert.Equal(3, result.GeneratedTypes.Distinct().Count());
        Assert.All(result.GeneratedTypes, generatedType =>
        {
            CardModel canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(generatedType));
            Assert.Equal(isColorless, canonical.IsColorless);
            if (requiredType is { } type)
            {
                Assert.Equal(type, canonical.Type);
            }
        });
        Assert.Equal(result.GeneratedTypes[0], result.SelectedType);
        // Three eligible cards: native full-pool Fisher–Yates consumes N-1 draws.
        Assert.Equal(2, result.GenerationCounter);
        Assert.Equal(0, result.SelectionCounter);
        Assert.Equal(PileType.Hand, result.SelectedPile);
        Assert.True(result.SelectedTemporaryFreeThisTurn);
        Assert.Equal(0, result.SelectedEnergyCost);
    }

    private static async Task<GenerationResult> UseGenerationPotionAsync<TPotion>(string seed)
        where TPotion : PotionModel
    {
        (Player player, CombatRoom room) = await CreateCombatAsync(seed);
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        PotionGenerationProbe.Reset();

        await UsePotionAsync(typeof(TPotion), player, player.Creature);

        CardModel selected = Assert.Single(player.PlayerCombatState!.Hand.Cards);
        return new GenerationResult(
            PotionGenerationProbe.ClonedTypes.ToArray(),
            selected.GetType(),
            room.Engine.State.RunState.Rng.CombatCardGeneration.Counter,
            room.Engine.State.RunState.Rng.CombatCardSelection.Counter,
            selected.Pile!.Type,
            selected.TemporaryFreeThisTurn,
            selected.EnergyCost);
    }

    private static async Task AddDamageModifiersAsync(
        CombatRoom room,
        Player player,
        Creature enemy)
    {
        await PowerCmd.Apply<StrengthPower>(
            room.Engine.State,
            player.Creature,
            50m,
            player.Creature,
            null);
        await PowerCmd.Apply<VulnerablePower>(
            room.Engine.State,
            enemy,
            3m,
            player.Creature,
            null);
    }

    private static async Task UsePotionAsync(
        Type potionType,
        Player player,
        Creature? target)
    {
        PotionModel canonical = ModelDb.GetById<PotionModel>(ModelDb.GetId(potionType));
        PotionModel potion = player.AddPotionInternal(canonical);
        await PotionCmd.Use(potion, player, target);
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<PotionTestCharacter>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }

    private sealed record GenerationResult(
        IReadOnlyList<Type> GeneratedTypes,
        Type SelectedType,
        int GenerationCounter,
        int SelectionCounter,
        PileType SelectedPile,
        bool SelectedTemporaryFreeThisTurn,
        int SelectedEnergyCost);
}

file static class PotionGenerationProbe
{
    private static readonly List<Type> Cloned = new();

    public static IReadOnlyList<Type> ClonedTypes => Cloned;

    public static void Record(Type type) => Cloned.Add(type);

    public static void Reset() => Cloned.Clear();
}

file sealed class PotionTestCharacter : CharacterModel
{
    public override int StartingHp => 75;

    public override int StartingGold => 99;

    // 偏离 #319：药水/卡牌生成改走 Character.CardPool 之后，测试替身角色必须提供卡池，
    // 否则落到 EmptyCardPool 会生成不出任何候选。
    private PotionTestCardPool? _cardPool;
    public override CardPoolModel CardPool => _cardPool ??= new PotionTestCardPool();
}

file abstract class PotionGenerationCandidate : CardModel
{
    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        PotionGenerationProbe.Record(GetType());
    }
}

file sealed class PotionAttackCandidateA : PotionGenerationCandidate
{
    public override CardType Type => CardType.Attack;
}

file sealed class PotionAttackCandidateB : PotionGenerationCandidate
{
    public override CardType Type => CardType.Attack;
}

file sealed class PotionAttackCandidateC : PotionGenerationCandidate
{
    public override CardType Type => CardType.Attack;
}

file sealed class PotionColorlessCandidateA : PotionGenerationCandidate
{
    public override CardType Type => CardType.Attack;

    public override bool IsColorless => true;
}

file sealed class PotionColorlessCandidateB : PotionGenerationCandidate
{
    public override CardType Type => CardType.Skill;

    public override bool IsColorless => true;
}

file sealed class PotionColorlessCandidateC : PotionGenerationCandidate
{
    public override CardType Type => CardType.Power;

    public override bool IsColorless => true;
}

file sealed class PotionTestCardPool : CardPoolModel
{
    public override IReadOnlyList<CardModel> AllCards =>
    [
        ModelDb.Card<PotionAttackCandidateA>(),
        ModelDb.Card<PotionAttackCandidateB>(),
        ModelDb.Card<PotionAttackCandidateC>(),
    ];
}
