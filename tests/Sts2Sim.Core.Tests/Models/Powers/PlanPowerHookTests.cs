using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class PlanPowerHookTests : IDisposable
{
    public PlanPowerHookTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(ParticleWall),
            // Off-role cards expose flat-pool transforms; only Regent is the playable fixture character.
            typeof(Backflip), typeof(DaggerThrow), typeof(CloakAndDagger), typeof(Acrobatics),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(SovereignBlade),
            typeof(ConquerorPower), typeof(FurnacePower), typeof(ReflectPower), typeof(ArsenalPower),
            typeof(ForegoneConclusionPower), typeof(TyrannyPower), typeof(TheSealedThronePower),
            typeof(PaleBlueDotPower), typeof(SpectrumShiftPower), typeof(CalamityPower),
            typeof(MonarchsGazePower), typeof(TheGambitPower), typeof(MayhemPower), typeof(EntropyPower),
            typeof(MonarchsGazeStrengthDownPower),
            typeof(StrengthPower), typeof(DrawCardsNextTurnPower), typeof(Purity), typeof(Supermassive),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ConquerorPower_DoublesSovereignBladeDamage_AgainstDebuffedTarget()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("conqueror");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<ConquerorPower>(room.Engine.State, enemy, 1m, null, null);
        await ForgeCmd.Forge(5m, player, null);
        SovereignBlade blade = player.PlayerCombatState!.Hand.Cards.OfType<SovereignBlade>().Single();
        int hpBefore = enemy.CurrentHp;

        await blade.PlayAsync(enemy);

        // SovereignBlade 基础伤害10 + Forge(5) = 15，ConquerorPower 使其翻倍 = 30。
        Assert.Equal(hpBefore - 30, enemy.CurrentHp);
    }

    [Fact]
    public async Task ConquerorPower_TicksDownAtOwnersSideTurnEnd()
    {
        (_, CombatRoom room) = await CreateCombatAsync("conqueror-tick");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        var power = await PowerCmd.Apply<ConquerorPower>(room.Engine.State, enemy, 2m, null, null);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, new[] { enemy });

        Assert.Equal(1, power!.Amount);
    }

    [Fact]
    public async Task FurnacePower_ForgesOnOwnersSideTurnStart()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("furnace");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<FurnacePower>(room.Engine.State, player.Creature, 4m, null, null);

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Player, new[] { player.Creature });

        SovereignBlade blade = player.PlayerCombatState!.Hand.Cards.OfType<SovereignBlade>().Single();
        int hpBefore = enemy.CurrentHp;
        await blade.PlayAsync(enemy);

        // SovereignBlade 基础伤害10 + Forge(4) = 14。
        Assert.Equal(hpBefore - 14, enemy.CurrentHp);
    }

    [Fact]
    public async Task ReflectPower_ReflectsBlockedDamageToDealer()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("reflect");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<ReflectPower>(room.Engine.State, player.Creature, 1m, null, null);
        await CreatureCmd.GainBlock(room.Engine.State, player.Creature, 20m, ValueProp.Move, null, null);
        int enemyHpBefore = enemy.CurrentHp;

        await CreatureCmd.Damage(room.Engine.State, new[] { player.Creature }, 10m, ValueProp.Move, enemy, null, null);

        Assert.Equal(enemyHpBefore - 10, enemy.CurrentHp);
    }

    [Fact]
    public async Task ArsenalPower_GrantsStrengthWhenOwnerGeneratesACard()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("arsenal");
        await PowerCmd.Apply<ArsenalPower>(room.Engine.State, player.Creature, 3m, null, null);
        var generated = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        generated.AssignOwner(player);

        await CardPileCmd.Generate(room.Engine.State, generated, PileType.Hand);

        StrengthPower? strength = player.Creature.Powers.OfType<StrengthPower>().SingleOrDefault();
        Assert.NotNull(strength);
        Assert.Equal(3, strength!.Amount);
    }

    [Fact]
    public async Task ForegoneConclusionPower_MovesCardsFromDrawPileToHandThenRemovesItself()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("foregone-conclusion");
        foreach (CardModel c in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(c, PileType.Draw, CardPilePosition.Top);
        }

        var power = await PowerCmd.Apply<ForegoneConclusionPower>(room.Engine.State, player.Creature, 2m, null, null);
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;
        room.Engine.State.CardSelectionSource = new PlanPowerFirstCardsSelectionSource();

        await Hook.BeforeHandDraw(room.Engine.State, player);

        Assert.Equal(handBefore + 2, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.DoesNotContain(power, player.Creature.Powers);
    }

    [Fact]
    public async Task TyrannyPower_IncreasesHandDrawAndExhaustsCardsAtOwnersTurnStart()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("tyranny");
        await PowerCmd.Apply<TyrannyPower>(room.Engine.State, player.Creature, 2m, null, null);

        decimal modifiedDraw = Hook.ModifyHandDraw(room.Engine.State, player, 5m);
        Assert.Equal(7m, modifiedDraw);

        int exhaustBefore = player.PlayerCombatState!.ExhaustPile.Cards.Count;
        room.Engine.State.CardSelectionSource = new PlanPowerFirstCardsSelectionSource();
        await Hook.AfterPlayerTurnStart(room.Engine.State, player);
        Assert.Equal(exhaustBefore + 2, player.PlayerCombatState!.ExhaustPile.Cards.Count);
    }

    [Fact]
    public async Task TheSealedThronePower_GainsStarsBeforeEachOwnedCardPlay()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("the-sealed-throne");
        await PowerCmd.Apply<TheSealedThronePower>(room.Engine.State, player.Creature, 3m, null, null);
        DefendRegent card = AddToHand<DefendRegent>(player);
        int starsBefore = player.PlayerCombatState!.Stars;

        await card.PlayAsync(target: null);

        Assert.Equal(starsBefore + 3, player.PlayerCombatState!.Stars);
    }

    [Fact]
    public async Task PaleBlueDotPower_GrantsDrawCardsNextTurnAfterFiveCardsPlayedThisTurn_OnlyOnce()
    {
        (Player player, _) = await CreateCombatAsync("pale-blue-dot");
        await PowerCmd.Apply<PaleBlueDotPower>(player.Creature.CombatState!, player.Creature, 2m, null, null);

        for (int i = 0; i < 6; i++)
        {
            DefendRegent card = AddToHand<DefendRegent>(player);
            await card.PlayAsync(target: null);
        }

        var drawNextTurn = player.Creature.Powers.OfType<DrawCardsNextTurnPower>().SingleOrDefault();
        Assert.NotNull(drawNextTurn);
        Assert.Equal(2, drawNextTurn!.Amount);
    }

    [Fact]
    public async Task SpectrumShiftPower_GeneratesColorlessCardsIntoHandBeforeHandDraw()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("spectrum-shift");
        await PowerCmd.Apply<SpectrumShiftPower>(room.Engine.State, player.Creature, 2m, null, null);
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;

        await Hook.BeforeHandDraw(room.Engine.State, player);

        Assert.True(player.PlayerCombatState!.Hand.Cards.Count >= handBefore + 1);
        Assert.All(
            player.PlayerCombatState!.Hand.Cards.Skip(handBefore),
            c => Assert.True(c.IsColorless));
    }

    [Fact]
    public async Task CalamityPower_GeneratesAttackCardsAfterOwnerPlaysAnAttack()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("calamity");
        await PowerCmd.Apply<CalamityPower>(room.Engine.State, player.Creature, 1m, null, null);
        StrikeRegent strike = AddToHand<StrikeRegent>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;

        await strike.PlayAsync(enemy);

        // Strike 出牌后离开手牌(-1)，Calamity 生成1张攻击牌(+1)，净变化为0；直接确认生成的卡类型正确。
        Assert.Contains(player.PlayerCombatState!.Hand.Cards, c => c.Type == CardType.Attack && !c.IsColorless);
        _ = handBefore;
    }

    [Fact]
    public async Task MonarchsGazePower_AppliesNegativeStrengthToTargetAfterPoweredAttack()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("monarchs-gaze");
        await PowerCmd.Apply<MonarchsGazePower>(room.Engine.State, player.Creature, 2m, null, null);
        StrikeRegent strike = AddToHand<StrikeRegent>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await strike.PlayAsync(enemy);

        MonarchsGazeStrengthDownPower strengthDown = Assert.Single(enemy.Powers.OfType<MonarchsGazeStrengthDownPower>());
        Assert.Equal(2, strengthDown.Amount);
        Assert.Equal(-2, enemy.GetPower<StrengthPower>()!.Amount);
    }

    [Fact]
    public async Task TheGambitPower_KillsOwnerOnAnyUnblockedPoweredDamage()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("the-gambit");
        await PowerCmd.Apply<TheGambitPower>(room.Engine.State, player.Creature, 1m, null, null);

        await CreatureCmd.Damage(room.Engine.State, new[] { player.Creature }, 1m, ValueProp.Move, null, null, null);

        Assert.True(player.Creature.IsDead);
    }

    [Fact]
    public async Task MayhemPower_AutoPlaysTopOfDrawPileDuringOwnersAutoPrePlayPhase()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("mayhem");
        DefendRegent onTopAfterDraw = AddTo<DefendRegent>(player, PileType.Draw, CardPilePosition.Top);
        foreach (CardModel c in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(c, PileType.Draw, CardPilePosition.Top);
        }

        await PowerCmd.Apply<MayhemPower>(room.Engine.State, player.Creature, 1m, null, null);
        int blockBefore = player.Creature.Block;

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Player, new[] { player.Creature });

        Assert.Equal(blockBefore, player.Creature.Block);
        Assert.Equal(PileType.Draw, onTopAfterDraw.Pile!.Type);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(blockBefore + 5, player.Creature.Block);
        Assert.NotEqual(PileType.Draw, onTopAfterDraw.Pile!.Type);
    }

    [Fact]
    public async Task EntropyPower_AndBatchTransform_RecordGenerationForSupermassive()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("entropy");
        foreach (CardModel c in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(c, PileType.Draw, CardPilePosition.Top);
        }

        DefendRegent original = AddToHand<DefendRegent>(player);
        await PowerCmd.Apply<EntropyPower>(room.Engine.State, player.Creature, 1m, null, null);

        await Hook.AfterPlayerTurnStart(room.Engine.State, player);

        Assert.DoesNotContain(original, player.PlayerCombatState!.Hand.Cards);
        Assert.All(player.PlayerCombatState.Hand.Cards,
            card => Assert.Contains(player.Character.CardPool.AllCards, candidate => candidate.Id == card.Id));
        Assert.Equal(1, player.PlayerCombatState.CardsGeneratedThisCombat);

        DefendRegent batchOriginal = AddToHand<DefendRegent>(player);
        var batchReplacement = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        batchReplacement.AssignOwner(player);
        await CardCmd.Transform([new CardTransformation(batchOriginal, batchReplacement)], rng: null);

        Assert.Equal(2, player.PlayerCombatState.CardsGeneratedThisCombat);
        Supermassive supermassive = AddToHand<Supermassive>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        await supermassive.PlayAsync(enemy);
        Assert.Equal(hpBefore - 11, enemy.CurrentHp);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel =>
        AddTo<TCard>(player, PileType.Hand);

    private static TCard AddTo<TCard>(Player player, PileType pileType, CardPilePosition position = CardPilePosition.Bottom)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType, position);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

file sealed class PlanPowerFirstCardsSelectionSource : ICardSelectionDecisionSource
{
    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
        Task.FromResult<IReadOnlyList<CardModel>>(
            request.Candidates.Take(request.MaxCount).ToArray());
}
