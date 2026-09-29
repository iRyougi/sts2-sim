using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Potions;

file sealed class Task12NormalCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
}

file sealed class Task12XCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
}

[Collection("ModelDb")]
public sealed class RarePotionBatch3Tests : IDisposable
{
    public RarePotionBatch3Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(RarePotionBatch3TestCharacter),
            typeof(Task12NormalCard),
            typeof(Task12XCard),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<string, TargetType> Metadata => new()
    {
        { "OrobicAcid", TargetType.AnyPlayer },
        { "ShacklingPotion", TargetType.AllEnemies },
        { "ShipInABottle", TargetType.AnyPlayer },
        { "SneckoOil", TargetType.AnyPlayer },
    };

    [Theory]
    [MemberData(nameof(Metadata))]
    public void Metadata_IsExactlyRareCombatOnlyWithSpecifiedTarget(string name, TargetType targetType)
    {
        PotionModel potion = GetCanonicalPotion(name);
        Assert.Equal(PotionRarity.Rare, potion.Rarity);
        Assert.Equal(PotionUsage.CombatOnly, potion.Usage);
        Assert.Equal(targetType, potion.TargetType);
    }

    [Fact]
    public async Task OrobicAcid_GeneratesOneOfEachTypeAllTemporaryFreeIntoHand()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("orobic");
        ClearCombatPiles(player);
        const ulong generationSeed = 20260924;
        room.Engine.State.RunState.Rng.MockRng(RunRngType.CombatCardGeneration, generationSeed);
        int rngBefore = room.Engine.State.RunState.Rng.CombatCardGeneration.Counter;

        // 原版对每种类型各调一次 GetDistinctForCombat(..., 1, rng)：整池 UnstableShuffle 后取第一张，
        // 每次消耗"候选数 - 1"个随机数。期望值用独立的 Fisher–Yates 镜像算，不经过被测的工厂。
        var mirror = new Rng(generationSeed);
        var expectedIds = new List<ModelId>();
        int expectedDraws = 0;
        foreach (CardType type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
        {
            List<CardModel> pool = CardPoolFilters.ForCombatGeneration(
                    player.Character.CardPool.GetUnlockedCards(player.UnlockState, isMultiplayer: false)
                        .Where(card => card.Type == type))
                .ToList();
            for (int n = pool.Count; n > 1;)
            {
                n--;
                int k = mirror.NextInt(n + 1);
                (pool[k], pool[n]) = (pool[n], pool[k]);
                expectedDraws++;
            }
            expectedIds.Add(pool[0].Id);
        }

        await UsePotionAsync("OrobicAcid", player, player.Creature);

        IReadOnlyList<CardModel> generated = player.PlayerCombatState!.Hand.Cards;
        Assert.Equal(expectedIds, generated.Select(card => card.Id));
        Assert.Equal(rngBefore + expectedDraws, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
        Assert.All(generated, card =>
        {
            Assert.False(card.IsColorless);
            Assert.True(card.CanBeGeneratedInCombat);
            // 原版 SetToFreeThisTurn 的能量部分是 SetThisTurnOrUntilPlayed(0)：打出即失效，不是整回合免费。
            Assert.Equal(0, card.TemporaryCostOverrideThisTurnOrUntilPlayed);
            Assert.False(card.TemporaryFreeThisTurn);
            Assert.Equal(0, card.EnergyCost);
        });
        player.PlayerCombatState.EndOfTurnCleanup();
        Assert.All(generated, card => Assert.Null(card.TemporaryCostOverrideThisTurnOrUntilPlayed));
    }

    [Fact]
    public async Task ShacklingPotion_HitsAllLivingEnemiesAndTemporaryPenaltyExpiresOnTheirSide()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("shackling");
        var state = (CombatState)room.Engine.State;
        Creature first = state.Enemies.Single();
        Creature second = state.AddMonster((WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(), CombatSide.Enemy);
        Creature dead = state.AddMonster((WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(), CombatSide.Enemy);
        dead.LoseHpInternal(dead.CurrentHp, ValueProp.Unpowered);

        await UsePotionAsync("ShacklingPotion", player, target: null);

        foreach (Creature enemy in new[] { first, second })
        {
            PowerModel shackling = Assert.Single(enemy.Powers, power => power.GetType() == RequireTask12Type("ShacklingPotionPower"));
            Assert.IsAssignableFrom<TemporaryStrengthPower>(shackling);
            Assert.Equal(PowerType.Debuff, shackling.Type);
            Assert.False(shackling.AllowNegative);
            Assert.Equal(7, shackling.Amount);
            Assert.Equal(-7, enemy.GetPower<StrengthPower>()!.Amount);
            Assert.Same(player.Creature, shackling.Applier);
            Assert.Equal(0m, shackling.ModifyDamageAdditive(player.Creature, 20m, ValueProp.Move, enemy, null, null));
            Assert.Equal(0m, shackling.ModifyDamageAdditive(player.Creature, 20m, ValueProp.Unpowered, enemy, null, null));
            Assert.Equal(0m, shackling.ModifyDamageAdditive(enemy, 20m, ValueProp.Move, player.Creature, null, null));
            await shackling.AfterSideTurnEnd(CombatSide.Player, state.Allies);
            Assert.Contains(shackling, enemy.Powers);
            await shackling.AfterSideTurnEnd(CombatSide.Enemy, state.Enemies);
            Assert.DoesNotContain(shackling, enemy.Powers);
        }
        Assert.DoesNotContain(dead.Powers, power => power.GetType() == RequireTask12Type("ShacklingPotionPower"));
    }

    [Fact]
    public async Task ShipInABottle_GrantsTenUnpoweredNowAndTenAfterNextBlockClear()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("ship");
        await PowerCmd.Apply<DexterityPower>(room.Engine.State, player.Creature, 5m, player.Creature, null);
        await UsePotionAsync("ShipInABottle", player, player.Creature);

        Assert.Equal(10, player.Creature.Block);
        BlockNextTurnPower next = Assert.Single(player.Creature.Powers.OfType<BlockNextTurnPower>());
        Assert.Equal(10, next.Amount);
        player.Creature.LoseBlockInternal(player.Creature.Block);
        await Hook.AfterBlockCleared(room.Engine.State, player.Creature);
        Assert.Equal(10, player.Creature.Block);
        Assert.DoesNotContain(next, player.Creature.Powers);
    }

    [Fact]
    public async Task SneckoOil_DrawsThenRandomizesEveryNonXHandCardWithDedicatedRng()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("snecko");
        ClearCombatPiles(player);
        AddCard<Task12NormalCard>(player, PileType.Hand);
        AddCard<Task12NormalCard>(player, PileType.Hand);
        // 原版只给 EnergyCost.GetWithModifiers(None) >= 0 的牌抽费用；Wound 基础费用 -1，不抽也不改。
        Wound wound = AddCard<Wound>(player, PileType.Hand);
        for (int index = 0; index < 6; index++) AddCard<Task12NormalCard>(player, PileType.Draw);
        Task12XCard xCard = AddCard<Task12XCard>(player, PileType.Draw);

        ulong costSeed = Enumerable.Range(0, 1_000).Select(value => (ulong)value).First(seed =>
        {
            var probe = new Rng(seed);
            int[] values = Enumerable.Range(0, 8).Select(_ => probe.NextInt(4)).ToArray();
            return values.Contains(0) && values.Contains(3);
        });
        room.Engine.State.RunState.Rng.MockRng(RunRngType.CombatEnergyCosts, costSeed);
        var expectedRng = new Rng(costSeed);
        int counterBefore = room.Engine.State.RunState.Rng.CombatEnergyCosts.Counter;
        await UsePotionAsync("SneckoOil", player, player.Creature);

        Assert.Equal(10, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Empty(player.PlayerCombatState.DrawPile.Cards);
        CardModel[] randomized = player.PlayerCombatState.Hand.Cards
            .Where(card => !card.CostsXEnergy && card.CanonicalEnergyCostValue >= 0)
            .ToArray();
        Assert.Equal(8, randomized.Length);
        int[] expectedCosts = randomized.Select(_ => expectedRng.NextInt(4)).ToArray();
        // 原版写入 SetThisTurnOrUntilPlayed：打出即失效，并覆盖先前的"打出前"修正。
        Assert.Equal(expectedCosts, randomized.Select(card => card.TemporaryCostOverrideThisTurnOrUntilPlayed!.Value).ToArray());
        Assert.All(randomized, card => Assert.Null(card.TemporaryCostOverrideThisTurn));
        Assert.Contains(0, expectedCosts);
        Assert.Contains(3, expectedCosts);
        Assert.Null(xCard.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Null(wound.TemporaryCostOverrideThisTurnOrUntilPlayed);
        Assert.Null(wound.TemporaryCostOverrideThisTurn);
        Assert.Equal(counterBefore + randomized.Length, room.Engine.State.RunState.Rng.CombatEnergyCosts.Counter);
        player.PlayerCombatState.EndOfTurnCleanup();
        Assert.All(randomized, card => Assert.Null(card.TemporaryCostOverrideThisTurnOrUntilPlayed));
    }

    [Fact]
    public async Task SneckoOil_RespectsHandCapBeforeRandomizingTheCurrentHand()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("snecko-cap");
        ClearCombatPiles(player);
        for (int index = 0; index < 9; index++) AddCard<Task12NormalCard>(player, PileType.Hand);
        for (int index = 0; index < 3; index++) AddCard<Task12NormalCard>(player, PileType.Draw);
        int counterBefore = room.Engine.State.RunState.Rng.CombatEnergyCosts.Counter;
        await UsePotionAsync("SneckoOil", player, player.Creature);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(2, player.PlayerCombatState.DrawPile.Cards.Count);
        Assert.All(player.PlayerCombatState.Hand.Cards, card => Assert.NotNull(card.TemporaryCostOverrideThisTurnOrUntilPlayed));
        Assert.Equal(counterBefore + CardPile.MaxCardsInHand, room.Engine.State.RunState.Rng.CombatEnergyCosts.Counter);
    }

    private static PotionModel GetCanonicalPotion(string name)
    {
        Type type = RequireTask12Type(name);
        return ModelDb.GetById<PotionModel>(ModelDb.GetId(type));
    }

    private static Type RequireTask12Type(string name)
    {
        string category = name.EndsWith("Power", StringComparison.Ordinal) ? "Powers" : "Potions";
        return typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.{category}.{name}")
            ?? throw new Xunit.Sdk.XunitException($"Task 12 model {name} is not implemented.");
    }

    private static async Task UsePotionAsync(string name, Player owner, Creature? target)
    {
        PotionModel potion = owner.AddPotionInternal(GetCanonicalPotion(name));
        await PotionCmd.Use(potion, owner, target);
    }

    private static TCard AddCard<TCard>(Player player, PileType pile) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
            foreach (CardModel card in pile.Cards.ToList())
                CardPileCmd.Remove(card);
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<RarePotionBatch3TestCharacter>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

file sealed class RarePotionBatch3TestCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;

    // 偏离 #319：药水/卡牌生成改走 Character.CardPool 之后，测试替身角色必须提供卡池，
    // 否则落到 EmptyCardPool 会生成不出任何候选。
    // 本夹具注册了 ContentRegistry.AllTypes，直接复用储君卡池即可覆盖 Attack/Skill/Power 三类。
    private RegentCardPool? _cardPool;
    public override CardPoolModel CardPool => _cardPool ??= new RegentCardPool();
}
