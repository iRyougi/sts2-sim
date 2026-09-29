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
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedCardRepairTask7FixRound1Tests : IDisposable
{
    public GeneratedCardRepairTask7FixRound1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(Task7FixHighHpMonster),
            typeof(Task7FixCostAttack),
            typeof(Task7FixXAttack),
            typeof(Task7FixZeroAttack),
            typeof(Task7FixUnplayableCard),
            typeof(Task7FixAnyEnemyCard),
            typeof(Task7FixAnyAllyCard),
            typeof(Task7FixAnyPlayerCard),
            typeof(Task7FixRoundProbeCard),
            typeof(Task7FixBatchAutoplayCard),
            typeof(Task7FixAutoPreProbePower),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VoidForm_LateEnergyPassBeatsTangledInEitherPowerOrder(bool voidFirst)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync($"task7-fix-cost-{voidFirst}", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Task7FixCostAttack first = AddTo<Task7FixCostAttack>(owner, PileType.Hand);
        Task7FixCostAttack second = AddTo<Task7FixCostAttack>(owner, PileType.Hand);
        Task7FixCostAttack third = AddTo<Task7FixCostAttack>(owner, PileType.Hand);
        Task7FixXAttack xAttack = AddTo<Task7FixXAttack>(owner, PileType.Hand);
        Task7FixZeroAttack zeroAttack = AddTo<Task7FixZeroAttack>(owner, PileType.Hand);
        Task7FixCostAttack temporaryFree = AddTo<Task7FixCostAttack>(owner, PileType.Hand);
        temporaryFree.MakeTemporaryFreeThisTurn();

        if (voidFirst)
        {
            await PowerCmd.Apply<VoidFormPower>(room.Engine.State, owner.Creature, 2m, owner.Creature, null);
            await PowerCmd.Apply<TangledPower>(room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        }
        else
        {
            await PowerCmd.Apply<TangledPower>(room.Engine.State, owner.Creature, 2m, owner.Creature, null);
            await PowerCmd.Apply<VoidFormPower>(room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        }
        await Hook.BeforeSideTurnStart(
            room.Engine.State, CombatSide.Player, room.Engine.State.Allies);

        owner.PlayerCombatState!.Energy = 0;
        await PlayerCmd.LoseStars(999, owner);
        Assert.Equal((0, 0), (first.EnergyCost, first.StarCost));
        Assert.True(first.CanPlay(out _));
        Assert.Equal(-1, xAttack.EnergyCost); // Native CostsX keeps this probe's -1 base cost.
        Assert.Equal(0, zeroAttack.EnergyCost);
        Assert.Equal(0, temporaryFree.EnergyCost);

        owner.PlayerCombatState.Energy = 10;
        await PlayerCmd.GainStars(10, owner);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await room.Engine.PlayCardAsync(owner, first, enemy);
        await room.Engine.PlayCardAsync(owner, second, enemy);

        Assert.Equal(new ResourceInfo(0, 0, 0, 0), first.LastPlay!.Resources);
        Assert.Equal(new ResourceInfo(0, 0, 0, 0), second.LastPlay!.Resources);
        Assert.Equal((4, 2), (third.EnergyCost, third.StarCost));
        await room.Engine.PlayCardAsync(owner, third, enemy);
        Assert.Equal(new ResourceInfo(4, 4, 2, 2), third.LastPlay!.Resources);
    }

    [Fact]
    public async Task AutoPlay_UnplayableMovesToResultWithoutExecutingOrCompleting()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-fix-unplayable", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        await PowerCmd.Apply<MonologuePower>(room.Engine.State, owner.Creature, 1m, owner.Creature, null);
        await PowerCmd.Apply<PaleBlueDotPower>(room.Engine.State, owner.Creature, 1m, owner.Creature, null);
        Task7FixUnplayableCard card = AddTo<Task7FixUnplayableCard>(
            owner, PileType.Draw, CardPilePosition.Top);

        IReadOnlyList<CardPlay> results =
            await AutoPlayCmd.FromTopOfDrawPileWithResults(room.Engine.State, owner, 1);

        Assert.Empty(results);
        Assert.False(card.WasPlayed);
        Assert.Equal(PileType.Discard, card.Pile!.Type);
        Assert.Same(owner, card.Owner);
        Assert.Equal(0, owner.PlayerCombatState!.CardsPlayedThisTurn);
        Assert.DoesNotContain(owner.Creature.Powers, power => power is StrengthPower);
        Assert.DoesNotContain(owner.Creature.Powers, power => power is DrawCardsNextTurnPower);
    }

    [Fact]
    public async Task AutoPlay_EndedCombatKeepsDrawCardWithoutCompleting()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-fix-no-enemy", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Creature enemy = room.Engine.State.Enemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp, ValueProp.Unblockable);
        Task7FixAnyEnemyCard card = AddTo<Task7FixAnyEnemyCard>(
            owner, PileType.Draw, CardPilePosition.Top);

        IReadOnlyList<CardPlay> results =
            await AutoPlayCmd.FromTopOfDrawPileWithResults(room.Engine.State, owner, 1);

        Assert.Empty(results);
        Assert.Null(card.LastPlay);
        Assert.Equal(PileType.Draw, card.Pile!.Type);
        Assert.Equal(0, owner.PlayerCombatState!.CardsPlayedThisTurn);
    }

    [Fact]
    public async Task AutoPlay_AnyAllyUsesLivingOtherPlayerAndExcludesOwner()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-fix-ally-target", 3);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Task7FixAnyAllyCard card = AddTo<Task7FixAnyAllyCard>(
            owner, PileType.Draw, CardPilePosition.Top);
        Creature[] expectedCandidates =
            { players[1].Creature, players[2].Creature };
        var expectedRng = room.Engine.State.RunState.Rng.CombatTargets.CloneExact();
        Creature expectedTarget = expectedRng.NextItem(expectedCandidates)!;

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, owner, 1);

        Assert.NotNull(card.LastPlay);
        Assert.Same(expectedTarget, card.LastPlay.Target);
        Assert.NotSame(owner.Creature, card.LastPlay.Target);
        Assert.Equal(1, owner.PlayerCombatState!.CardsPlayedThisTurn);
        Assert.Equal(
            expectedRng.NextInt(1000),
            room.Engine.State.RunState.Rng.CombatTargets.NextInt(1000));
    }

    [Fact]
    public async Task AutoPlay_AnyAllyWithoutOtherLivingPlayerMovesToResult()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-fix-no-ally", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Task7FixAnyAllyCard card = AddTo<Task7FixAnyAllyCard>(
            owner, PileType.Draw, CardPilePosition.Top);

        IReadOnlyList<CardPlay> results =
            await AutoPlayCmd.FromTopOfDrawPileWithResults(room.Engine.State, owner, 1);

        Assert.Empty(results);
        Assert.Null(card.LastPlay);
        Assert.Equal(PileType.Discard, card.Pile!.Type);
        Assert.Equal(0, owner.PlayerCombatState!.CardsPlayedThisTurn);
    }

    [Fact]
    public async Task AutoPlay_AnyPlayerKeepsAuthoritativeNullCardTarget()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-fix-any-player", 2);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Task7FixAnyPlayerCard card = AddTo<Task7FixAnyPlayerCard>(
            owner, PileType.Draw, CardPilePosition.Top);

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, owner, 1);

        Assert.NotNull(card.LastPlay);
        Assert.Null(card.LastPlay.Target);
        Assert.Equal(1, owner.PlayerCombatState!.CardsPlayedThisTurn);
    }

    [Fact]
    public async Task Mayhem_VoidFormDefersTurnEndUntilAllAutoPrePlayHooksAndPlayPhaseTransition()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-fix-mayhem-void", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        await PowerCmd.Apply<MayhemPower>(room.Engine.State, owner.Creature, 1m, owner.Creature, null);
        await PowerCmd.Apply<Task7FixAutoPreProbePower>(
            room.Engine.State, owner.Creature, 1m, owner.Creature, null);
        for (int i = 0; i < 5; i++)
        {
            AddTo<Task7FixRoundProbeCard>(owner, PileType.Draw);
        }
        AddTo<VoidForm>(owner, PileType.Draw);
        Task7FixAutoPreProbePower probe =
            Assert.Single(owner.Creature.Powers.OfType<Task7FixAutoPreProbePower>());

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(new[] { 2, 3 }, probe.ObservedRounds);
        Assert.All(probe.ObservedPhases, phase => Assert.Equal(PlayerTurnPhase.AutoPrePlay, phase));
        Assert.Equal(3, room.Engine.State.RoundNumber);
        Assert.Equal(PlayerTurnPhase.Play, owner.PlayerCombatState!.Phase);
    }

    [Fact]
    public async Task NestedAutoplayBatch_VoidFormEndsTurnOnlyAfterRemainingCardsAndOuterCardComplete()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-fix-nested-batch", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Task7FixRoundProbeCard probe = AddTo<Task7FixRoundProbeCard>(
            owner, PileType.Draw, CardPilePosition.Top);
        AddTo<VoidForm>(owner, PileType.Draw, CardPilePosition.Top);
        Task7FixBatchAutoplayCard batch = AddTo<Task7FixBatchAutoplayCard>(owner, PileType.Hand);

        await room.Engine.PlayCardAsync(owner, batch, null);

        Assert.Equal(1, probe.RoundWhenPlayed);
        Assert.Equal(PlayerTurnPhase.Play, probe.PhaseWhenPlayed);
        Assert.Equal(1, batch.RoundAfterBatch);
        Assert.Equal(2, room.Engine.State.RoundNumber);
        Assert.Equal(PlayerTurnPhase.Play, owner.PlayerCombatState!.Phase);
    }

    [Fact]
    public async Task DirectAutoplayBatch_VoidFormDefersTurnEndUntilWholeBatchCompletes()
    {
        (Player[] players, CombatRoom room) =
            await CreateCombatAsync("task7-fix-direct-batch", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Task7FixRoundProbeCard probe = AddTo<Task7FixRoundProbeCard>(
            owner, PileType.Draw, CardPilePosition.Top);
        AddTo<VoidForm>(owner, PileType.Draw, CardPilePosition.Top);

        var results = await AutoPlayCmd.FromTopOfDrawPileWithResults(
            room.Engine.State, owner, 2);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.True(result.IsAutoPlay));
        Assert.Equal(1, probe.RoundWhenPlayed);
        Assert.Equal(PlayerTurnPhase.Play, probe.PhaseWhenPlayed);
        Assert.Equal(2, room.Engine.State.RoundNumber);
        Assert.Equal(PlayerTurnPhase.Play, owner.PlayerCombatState!.Phase);
    }

    [Fact]
    public async Task DirectVoidForm_EndsExactlyOnceAfterItsCompletedCardAction()
    {
        var observer = new Task7FixObserver();
        (Player[] players, CombatRoom room) =
            await CreateCombatAsync("task7-fix-direct-void", 1, observer);
        Player owner = players[0];
        observer.Events.Clear();

        await room.Engine.PlayCardAsync(owner, AddTo<VoidForm>(owner, PileType.Hand), null);

        Assert.Equal(new[] { "void-finished", "turn-ended", "turn-started" }, observer.Events);
        Assert.Equal(2, room.Engine.State.RoundNumber);
        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.Equal(PlayerTurnPhase.Play, owner.PlayerCombatState!.Phase);
        Assert.Single(owner.Creature.Powers.OfType<VoidFormPower>());
    }

    private static TCard AddTo<TCard>(
        Player player,
        PileType pileType,
        CardPilePosition position = CardPilePosition.Bottom)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType, position);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.AllPiles
                     .SelectMany(pile => pile.Cards).ToList())
        {
            CardPileCmd.Remove(card);
        }
    }

    private static async Task<(Player[] Players, CombatRoom Room)> CreateCombatAsync(
        string seed,
        int playerCount,
        ICombatObserver? observer = null)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player[] players = Enumerable.Range(0, playerCount)
            .Select(_ => Player.CreateForNewRun(ModelDb.Character<Regent>(), runState))
            .ToArray();
        foreach (Player player in players)
        {
            runState.AddPlayer(player);
        }

        var room = new CombatRoom(() =>
            (MonsterModel)ModelDb.Monster<Task7FixHighHpMonster>().MutableClone());
        if (observer is not null)
        {
            room.ConfigureObserver(observer);
        }
        await room.Enter(runState);
        return (players, room);
    }
}

file sealed class Task7FixCostAttack : CardModel
{
    public CardPlay? LastPlay { get; private set; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override int CanonicalStarCost => 2;
    protected override Task OnPlay(CardPlay cardPlay)
    {
        LastPlay = cardPlay;
        return Task.CompletedTask;
    }
}

file sealed class Task7FixXAttack : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => -1;
    protected override bool IsXEnergyCost => true;
}

file sealed class Task7FixZeroAttack : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task7FixUnplayableCard : CardModel
{
    public bool WasPlayed { get; private set; }
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };
    protected override Task OnPlay(CardPlay cardPlay)
    {
        WasPlayed = true;
        return Task.CompletedTask;
    }
}

file abstract class Task7FixTargetCard : CardModel
{
    public CardPlay? LastPlay { get; private set; }
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    protected override int CanonicalEnergyCost => 0;
    protected override Task OnPlay(CardPlay cardPlay)
    {
        LastPlay = cardPlay;
        return Task.CompletedTask;
    }
}

file sealed class Task7FixAnyEnemyCard : Task7FixTargetCard
{
    public override TargetType TargetType => TargetType.AnyEnemy;
}

file sealed class Task7FixAnyAllyCard : Task7FixTargetCard
{
    public override TargetType TargetType => TargetType.AnyAlly;
}

file sealed class Task7FixAnyPlayerCard : Task7FixTargetCard
{
    public override TargetType TargetType => TargetType.AnyPlayer;
}

file sealed class Task7FixRoundProbeCard : CardModel
{
    public int RoundWhenPlayed { get; private set; }
    public PlayerTurnPhase? PhaseWhenPlayed { get; private set; }
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override Task OnPlay(CardPlay cardPlay)
    {
        RoundWhenPlayed = Owner.Creature.CombatState!.RoundNumber;
        PhaseWhenPlayed = Owner.PlayerCombatState!.Phase;
        return Task.CompletedTask;
    }
}

file sealed class Task7FixBatchAutoplayCard : CardModel
{
    public int RoundAfterBatch { get; private set; }
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await AutoPlayCmd.FromTopOfDrawPile(CombatState!, Owner, 2);
        RoundAfterBatch = Owner.Creature.CombatState!.RoundNumber;
    }
}

file sealed class Task7FixAutoPreProbePower : PowerModel
{
    public List<int> ObservedRounds { get; private set; } = new();
    public List<PlayerTurnPhase> ObservedPhases { get; private set; } = new();
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override Task AfterAutoPrePlayPhaseEntered(Player player)
    {
        if (player == Owner.Player)
        {
            ObservedRounds.Add(Owner.CombatState!.RoundNumber);
            ObservedPhases.Add(player.PlayerCombatState!.Phase);
        }
        return Task.CompletedTask;
    }
    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        ObservedRounds = new List<int>(ObservedRounds);
        ObservedPhases = new List<PlayerTurnPhase>(ObservedPhases);
    }
}

file sealed class Task7FixObserver : ICombatObserver
{
    public List<string> Events { get; } = new();
    public void CombatStarted(CombatState state) { }
    public void PlayerTurnStarted(CombatState state) => Events.Add("turn-started");
    public void CardDrawn(CardModel card) { }
    public void CardPlayStarted(CardModel card, Creature? target) { }
    public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay)
    {
        if (card is VoidForm)
        {
            Events.Add("void-finished");
        }
    }
    public void EnemyMoveStarted(Creature source, string moveId) { }
    public void EnemyMoveFinished(Creature source, string moveId) { }
    public void PlayerTurnEnded(CombatState state) => Events.Add("turn-ended");
    public void PotionUseStarted(PotionModel potion, Creature? target) { }
    public void PotionUseFinished(PotionModel potion, Creature? target) { }
}

file sealed class Task7FixHighHpMonster : MonsterModel
{
    public override int MinInitialHp => 500;
    public override int MaxInitialHp => 500;
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("WAIT", _ => Task.CompletedTask, new SingleAttackIntent(0));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}
