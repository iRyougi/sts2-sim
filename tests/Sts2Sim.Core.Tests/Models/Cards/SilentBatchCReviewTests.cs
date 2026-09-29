namespace Sts2Sim.Core.Tests.Models.Cards;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class SilentBatchCReviewTests : IDisposable
{
    private static readonly Type[] BatchTypes =
    [
        typeof(Silent), typeof(RingOfTheSnake), typeof(WanderingGrunt),
        typeof(StrikeSilent), typeof(DefendSilent), typeof(Neutralize), typeof(Survivor),
        typeof(Shiv), typeof(WeakPower), typeof(StrengthPower), typeof(ArsenalPower),
        typeof(Acrobatics), typeof(BladeSymphony), typeof(CalculatedGamble),
        typeof(Prepared), typeof(Reflex), typeof(Tactician), typeof(Inky),
        typeof(BatchCReviewProbeRelic), typeof(BatchCMultiHitAttack),
        typeof(BatchCAllEnemyAttack),
    ];

    public SilentBatchCReviewTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(BatchTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Acrobatics_DiscardingReflex_AutoplaysForFreeWithNormalHooks()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("acrobatics-reflex");
        BatchCReviewProbeRelic probe = AttachProbe(player);
        Acrobatics acrobatics = AddToPile<Acrobatics>(player, PileType.Hand);
        Reflex reflex = AddToPile<Reflex>(player, PileType.Draw);
        AddDrawCards(player, 4);
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(reflex);
        player.PlayerCombatState!.Energy = 1;

        await room.Engine.PlayCardAsync(player, acrobatics, null);

        Assert.Equal(0, player.PlayerCombatState.Energy);
        Assert.Contains(reflex, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(4, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Contains("play:Reflex:auto=True", probe.Events);
    }

    [Fact]
    public async Task Prepared_DiscardingTactician_AutoplaysForFreeAndGainsEnergy()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("prepared-tactician");
        BatchCReviewProbeRelic probe = AttachProbe(player);
        Prepared prepared = AddToPile<Prepared>(player, PileType.Hand);
        Tactician tactician = AddToPile<Tactician>(player, PileType.Draw);
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(tactician);
        player.PlayerCombatState!.Energy = 0;

        await room.Engine.PlayCardAsync(player, prepared, null);

        Assert.Equal(1, player.PlayerCombatState.Energy);
        Assert.Contains(tactician, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Contains("play:Tactician:auto=True", probe.Events);
    }

    [Fact]
    public async Task CalculatedGamble_AutoplaysSlyCardsAfterDrawAndOrderedDiscardHooks()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("gamble-sly-order");
        BatchCReviewProbeRelic probe = AttachProbe(player);
        CalculatedGamble gamble = AddToPile<CalculatedGamble>(player, PileType.Hand);
        Reflex reflex = AddToPile<Reflex>(player, PileType.Hand);
        Tactician tactician = AddToPile<Tactician>(player, PileType.Hand);
        AddDrawCards(player, 4);
        player.PlayerCombatState!.Energy = 0;

        await room.Engine.PlayCardAsync(player, gamble, null);

        Assert.Equal(4, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(1, player.PlayerCombatState.Energy);
        Assert.Contains(reflex, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Contains(tactician, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Equal(
        [
            "discard:Reflex:hand=2",
            "discard:Tactician:hand=2",
            "play:Reflex:auto=True",
            "play:Tactician:auto=True",
        ], probe.Events);
    }

    [Fact]
    public async Task BladeSymphony_GeneratedShivsUseReceivingTeammateAsCreator()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateMultiplayerCombatAsync("blade-symphony-creator", playerCount: 3);
        foreach (Player player in players)
        {
            await PowerCmd.Apply<ArsenalPower>(
                room.Engine.State, player.Creature, 1m, player.Creature, null);
        }
        BladeSymphony card = AddToPile<BladeSymphony>(players[0], PileType.Hand);

        await room.Engine.PlayCardAsync(players[0], card, null);

        Assert.Empty(players[0].Creature.Powers.OfType<StrengthPower>());
        Assert.All(players.Skip(1), player =>
            Assert.Equal(2m, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount));
    }

    [Fact]
    public async Task Inky_MultiHitAppliesWeakOnceAfterAllDamagePostHooks()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("inky-multi-hit");
        BatchCReviewProbeRelic probe = AttachProbe(player);
        BatchCMultiHitAttack card = AddToPile<BatchCMultiHitAttack>(player, PileType.Hand);
        await CardCmd.Enchant<Inky>(card, 1m);
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await room.Engine.PlayCardAsync(player, card, enemy);

        Assert.Equal(1m, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount);
        Assert.False(probe.SawWeakDuringDamagePost);
    }

    [Fact]
    public async Task Inky_AllEnemiesAppliesWeakOnceToEveryEnemy()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("inky-all-enemies", enemyCount: 2);
        BatchCAllEnemyAttack card = AddToPile<BatchCAllEnemyAttack>(player, PileType.Hand);
        await CardCmd.Enchant<Inky>(card, 1m);

        await room.Engine.PlayCardAsync(player, card, null);

        Assert.Equal(2, room.Engine.State.HittableEnemies.Count);
        Assert.All(room.Engine.State.HittableEnemies, enemy =>
            Assert.Equal(1m, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount));
    }

    private static BatchCReviewProbeRelic AttachProbe(Player player)
    {
        var probe = (BatchCReviewProbeRelic)ModelDb.Relic<BatchCReviewProbeRelic>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        return probe;
    }

    private static TCard AddToPile<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private static void AddDrawCards(Player player, int count)
    {
        for (int i = 0; i < count; i++)
        {
            AddToPile<StrikeSilent>(player, PileType.Draw);
        }
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(
        string seed,
        int enemyCount = 1)
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateMultiplayerCombatAsync(seed, playerCount: 1, enemyCount);
        return (players[0], room);
    }

    private static async Task<(IReadOnlyList<Player> Players, CombatRoom Room)>
        CreateMultiplayerCombatAsync(string seed, int playerCount, int enemyCount = 1)
    {
        var runState = new RunState(seed, new Overgrowth());
        var players = new List<Player>();
        for (int i = 0; i < playerCount; i++)
        {
            Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
            runState.AddPlayer(player);
            players.Add(player);
        }

        Func<IReadOnlyList<MonsterModel>> monsterFactory = () => Enumerable
            .Range(0, enemyCount)
            .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone())
            .ToArray();
        var room = new CombatRoom(monsterFactory);
        await room.Enter(runState);
        foreach (Player player in players)
        {
            foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
            {
                foreach (CardModel card in pile.Cards.ToArray())
                {
                    CardPileCmd.Remove(card);
                }
            }
            player.PlayerCombatState.Energy = 10;
        }
        return (players, room);
    }

    private sealed class FixedSelectionSource(params CardModel[] selected)
        : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Assert.All(selected, card => Assert.Contains(card, request.Candidates));
            Assert.InRange(selected.Length, request.MinCount, request.MaxCount);
            return Task.FromResult<IReadOnlyList<CardModel>>(selected);
        }
    }
}

internal sealed class BatchCReviewProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.None;

    public List<string> Events { get; private set; } = new();

    public bool SawWeakDuringDamagePost { get; private set; }

    public override Task AfterCardDiscarded(CardModel card)
    {
        if (card is Reflex or Tactician)
        {
            Events.Add($"discard:{card.GetType().Name}:hand={card.Owner.PlayerCombatState!.Hand.Cards.Count}");
        }
        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card is Reflex or Tactician)
        {
            Events.Add($"play:{cardPlay.Card.GetType().Name}:auto={cardPlay.IsAutoPlay}");
        }
        return Task.CompletedTask;
    }

    public override Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (cardSource is BatchCMultiHitAttack && target.Powers.OfType<WeakPower>().Any())
        {
            SawWeakDuringDamagePost = true;
        }
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        Events = new List<string>();
        SawWeakDuringDamagePost = false;
    }
}

internal sealed class BatchCMultiHitAttack : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        return DamageCmd.Attack(1m)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(3)
            .Execute();
    }
}

internal sealed class BatchCAllEnemyAttack : CardModel
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay) => DamageCmd.Attack(1m)
        .FromCard(this, cardPlay)
        .TargetingAllOpponents(CombatState!)
        .Execute();
}
