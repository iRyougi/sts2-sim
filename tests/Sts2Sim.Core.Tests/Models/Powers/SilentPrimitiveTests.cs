namespace Sts2Sim.Core.Tests.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class SilentPrimitiveTests : IDisposable
{
    public SilentPrimitiveTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(PoisonPower),
            typeof(IntangiblePower),
            typeof(AccelerantPower),
            typeof(FanOfKnivesPower),
            typeof(Shiv),
            typeof(ShivGenerationProbePower),
            typeof(SlyKeywordProbeCard),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Poison_DealsAmountDamageAtTurnStart_ThenDecrements()
    {
        (_, CombatRoom room) = await CreateCombatAsync("silent-poison-damage");
        Creature target = room.Engine.State.HittableEnemies.Single();
        PoisonPower poison = (await PowerCmd.Apply<PoisonPower>(room.Engine.State, target, 3m, null, null))!;
        int hpBefore = target.CurrentHp;

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Enemy, new[] { target });

        Assert.Equal(hpBefore - 3, target.CurrentHp);
        Assert.Equal(2, poison.Amount);
    }

    [Fact]
    public async Task Poison_IgnoresBlock()
    {
        (_, CombatRoom room) = await CreateCombatAsync("silent-poison-block");
        Creature target = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<PoisonPower>(room.Engine.State, target, 3m, null, null);
        await CreatureCmd.GainBlock(room.Engine.State, target, 10m, ValueProp.Move, null, null);
        int hpBefore = target.CurrentHp;

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Enemy, new[] { target });

        Assert.Equal(hpBefore - 3, target.CurrentHp);
        Assert.Equal(10, target.Block);
    }

    [Fact]
    public async Task Poison_IgnoresStrength()
    {
        (_, CombatRoom room) = await CreateCombatAsync("silent-poison-strength");
        Creature target = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<PoisonPower>(room.Engine.State, target, 3m, null, null);
        await PowerCmd.Apply<StrengthPower>(room.Engine.State, target, 99m, null, null);
        int hpBefore = target.CurrentHp;

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Enemy, new[] { target });

        Assert.Equal(hpBefore - 3, target.CurrentHp);
    }

    [Fact]
    public async Task Poison_KillsOwner_StopsDecrementing()
    {
        (_, CombatRoom room) = await CreateCombatAsync("silent-poison-kill");
        Creature target = room.Engine.State.HittableEnemies.Single();
        target.LoseHpInternal(target.CurrentHp - 3, ValueProp.Unpowered);
        PoisonPower poison = (await PowerCmd.Apply<PoisonPower>(
            room.Engine.State,
            target,
            3m,
            null,
            null))!;

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Enemy, new[] { target });

        Assert.False(target.IsAlive);
        Assert.Equal(3, poison.Amount);
        Assert.DoesNotContain(target.Powers, power => power is PoisonPower);
    }

    [Fact]
    public async Task Poison_CalculateTotalDamageNextTurn_MatchesActualLoss()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("silent-poison-preview");
        Creature target = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<AccelerantPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);
        await PowerCmd.Apply<IntangiblePower>(room.Engine.State, target, 2m, null, null);
        PoisonPower poison = (await PowerCmd.Apply<PoisonPower>(room.Engine.State, target, 3m, null, null))!;
        int hpBefore = target.CurrentHp;

        int preview = poison.CalculateTotalDamageNextTurn();
        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Enemy, new[] { target });

        Assert.Equal(preview, hpBefore - target.CurrentHp);
    }

    [Fact]
    public async Task Intangible_CapsIncomingDamageAtOne()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("silent-intangible-cap");
        Creature target = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<IntangiblePower>(room.Engine.State, target, 2m, null, null);
        int hpBefore = target.CurrentHp;

        await CreatureCmd.Damage(room.Engine.State, new[] { target }, 20m, ValueProp.Move, player.Creature, null, null);

        Assert.Equal(hpBefore - 1, target.CurrentHp);
    }

    [Fact]
    public async Task Intangible_DoesNotCapDamageToOthers()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("silent-intangible-others");
        Creature protectedTarget = room.Engine.State.HittableEnemies.Single();
        Creature otherTarget = AddEnemy(room);
        await PowerCmd.Apply<IntangiblePower>(room.Engine.State, protectedTarget, 2m, null, null);
        int hpBefore = otherTarget.CurrentHp;

        await CreatureCmd.Damage(room.Engine.State, new[] { otherTarget }, 20m, ValueProp.Move, player.Creature, null, null);

        Assert.Equal(hpBefore - 20, otherTarget.CurrentHp);
    }

    [Fact]
    public async Task Intangible_DecrementsAtEnemyTurnEnd()
    {
        (_, CombatRoom room) = await CreateCombatAsync("silent-intangible-duration");
        Creature owner = room.Engine.State.HittableEnemies.Single();
        IntangiblePower intangible = (await PowerCmd.Apply<IntangiblePower>(room.Engine.State, owner, 2m, null, null))!;

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(2, intangible.Amount);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, new[] { owner });
        Assert.Equal(1, intangible.Amount);
    }

    [Fact]
    public async Task Shiv_DealsFourDamage_AndExhausts()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("silent-shiv-damage");
        Creature target = room.Engine.State.HittableEnemies.Single();
        Shiv shiv = await GenerateShivInHand(player, room.Engine.State);
        int hpBefore = target.CurrentHp;

        await shiv.PlayAsync(target);

        Assert.Equal(hpBefore - 4, target.CurrentHp);
        Assert.Equal(PileType.Exhaust, shiv.Pile!.Type);
    }

    [Fact]
    public async Task Shiv_Upgraded_DealsSix()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("silent-shiv-upgrade");
        Creature target = room.Engine.State.HittableEnemies.Single();
        Shiv shiv = await GenerateShivInHand(player, room.Engine.State);
        shiv.Upgrade();
        int hpBefore = target.CurrentHp;

        await shiv.PlayAsync(target);

        Assert.Equal(hpBefore - 6, target.CurrentHp);
    }

    [Fact]
    public async Task Shiv_WithFanOfKnives_TargetsAllEnemies()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("silent-shiv-fan");
        await PowerCmd.Apply<FanOfKnivesPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);
        Shiv shiv = await GenerateShivInHand(player, room.Engine.State);

        Assert.Equal(TargetType.AllEnemies, shiv.TargetType);
    }

    [Fact]
    public void Shiv_TargetType_IsAnyEnemyWhenCanonicalOrUnowned()
    {
        Shiv canonical = ModelDb.Card<Shiv>();
        Shiv unowned = (Shiv)canonical.MutableClone();

        Assert.Equal(TargetType.AnyEnemy, canonical.TargetType);
        Assert.Equal(TargetType.AnyEnemy, unowned.TargetType);
    }

    [Fact]
    public async Task Shiv_CreateInHand_GeneratesSingleAndMultipleCardsForOwnerAndCreator()
    {
        (Player owner, Player creator, CombatRoom room) = await CreateTwoPlayerCombatAsync("silent-shiv-create");
        ShivGenerationProbePower probe = (await PowerCmd.Apply<ShivGenerationProbePower>(
            room.Engine.State,
            creator.Creature,
            1m,
            creator.Creature,
            null))!;

        CardModel? single = await Shiv.CreateInHand(owner, room.Engine.State, creator);
        Shiv[] multiple = (await Shiv.CreateInHand(owner, 2, room.Engine.State, creator))
            .Cast<Shiv>()
            .ToArray();

        Assert.IsType<Shiv>(single);
        Assert.Equal(2, multiple.Length);
        Assert.All(new[] { single! }.Concat(multiple), shiv =>
        {
            Assert.Same(owner, shiv.Owner);
            Assert.Equal(PileType.Hand, shiv.Pile!.Type);
        });
        Assert.Equal(3, probe.GeneratedCount);
        Assert.Same(creator, probe.LastCreator);
    }

    [Fact]
    public async Task Shiv_CreateInHand_FullHandRedirectsEveryGeneratedCardToDiscard()
    {
        (Player owner, CombatRoom room) = await CreateCombatAsync("silent-shiv-full-hand");
        while (owner.PlayerCombatState!.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            var filler = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
            filler.AssignOwner(owner);
            CardPileCmd.Add(filler, PileType.Hand);
        }

        Shiv[] generated = (await Shiv.CreateInHand(owner, 2, room.Engine.State))
            .Cast<Shiv>()
            .ToArray();

        Assert.Equal(2, generated.Length);
        Assert.All(generated, shiv =>
        {
            Assert.Same(owner, shiv.Owner);
            Assert.Equal(PileType.Discard, shiv.Pile!.Type);
        });
    }

    [Fact]
    public void Sly_KeywordExists_AndSurvivesRoundTrip()
    {
        SlyKeywordProbeCard card = (SlyKeywordProbeCard)ModelDb.Card<SlyKeywordProbeCard>().MutableClone();

        Assert.Contains(CardKeyword.Sly, card.Keywords);
        Assert.True(card.HasKeyword(CardKeyword.Sly));
    }

    private static async Task<Shiv> GenerateShivInHand(Player player, ICombatState combatState)
    {
        var shiv = (Shiv)ModelDb.Card<Shiv>().MutableClone();
        shiv.AssignOwner(player);
        await CardPileCmd.Generate(combatState, shiv, PileType.Hand);
        return shiv;
    }

    private static Creature AddEnemy(CombatRoom room) => room.Engine.State.AddMonster(
        (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
        CombatSide.Enemy);

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }

    private static async Task<(Player Owner, Player Creator, CombatRoom Room)> CreateTwoPlayerCombatAsync(
        string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player creator = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(creator);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (owner, creator, room);
    }
}

file sealed class ShivGenerationProbePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public int GeneratedCount { get; private set; }
    public Player? LastCreator { get; private set; }

    public override Task AfterCardGenerated(CardModel card, Player? creator)
    {
        if (card is Shiv)
        {
            GeneratedCount++;
            LastCreator = creator;
        }

        return Task.CompletedTask;
    }
}

file sealed class SlyKeywordProbeCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
}
