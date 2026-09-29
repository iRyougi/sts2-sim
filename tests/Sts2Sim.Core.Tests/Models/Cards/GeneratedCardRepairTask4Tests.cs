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
public sealed class GeneratedCardRepairTask4Tests : IDisposable
{
    public GeneratedCardRepairTask4Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(Task4HighHpMonster),
            typeof(Task4ProbeAttack),
            typeof(Task4ThreeHitAttack),
            typeof(Task4ReplayProbeAttack),
            typeof(Task4UnpoweredAttack),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 9, 11)]
    [InlineData(true, 15, 5)]
    public async Task DarkShackles_AppliesTemporaryStrengthLossUntilTargetsSideEnds(
        bool upgraded,
        int expectedAmount,
        int expectedDamage)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task4-dark-shackles-{upgraded}", 1, 1);
        Player owner = players[0];
        Creature target = room.Engine.State.Enemies.Single();
        DarkShackles card = AddToHand<DarkShackles>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, card, target);

        PowerModel temporaryLoss = Assert.Single(
            target.Powers,
            power => power.GetType().Name == "DarkShacklesPower");
        Assert.Equal(expectedAmount, temporaryLoss.Amount);
        int hpBefore = owner.Creature.CurrentHp;
        await DamageCmd.Attack(20m).FromMonster(target.Monster!).Execute();
        Assert.Equal(hpBefore - expectedDamage, owner.Creature.CurrentHp);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Contains(temporaryLoss, target.Powers);
        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, room.Engine.State.Enemies);
        Assert.DoesNotContain(temporaryLoss, target.Powers);

        hpBefore = owner.Creature.CurrentHp;
        await DamageCmd.Attack(20m).FromMonster(target.Monster!).Execute();
        Assert.Equal(hpBefore - 20, owner.Creature.CurrentHp);
    }

    [Fact]
    public async Task DarkShackles_RepeatedApplicationsStackBeforeExpiringTogether()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            "task4-dark-shackles-stack", 1, 1);
        Player owner = players[0];
        Creature target = room.Engine.State.Enemies.Single();

        await room.Engine.PlayCardAsync(owner, AddToHand<DarkShackles>(owner), target);
        await room.Engine.PlayCardAsync(owner, AddToHand<DarkShackles>(owner), target);

        PowerModel power = Assert.Single(
            target.Powers,
            candidate => candidate.GetType().Name == "DarkShacklesPower");
        Assert.Equal(18, power.Amount);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, room.Engine.State.Enemies);
        Assert.DoesNotContain(power, target.Powers);
    }

    [Theory]
    [InlineData(false, 10, 2, 10)]
    [InlineData(true, 14, 3, 15)]
    public async Task Knockdown_MultipliesOnlyOtherAppliersPoweredAttackAndExpiresWithTargetSide(
        bool upgraded,
        int expectedCardDamage,
        int expectedMultiplier,
        int expectedAllyDamage)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task4-knockdown-{upgraded}", 2, 1);
        Player owner = players[0];
        Player ally = players[1];
        Creature target = room.Engine.State.Enemies.Single();
        Knockdown card = AddToHand<Knockdown>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }
        int hpBefore = target.CurrentHp;

        await room.Engine.PlayCardAsync(owner, card, target);

        Assert.Equal(hpBefore - expectedCardDamage, target.CurrentHp);
        PowerModel knockdown = Assert.Single(
            target.Powers,
            power => power.GetType().Name == "KnockdownPower");
        Assert.Equal(expectedMultiplier, knockdown.Amount);

        hpBefore = target.CurrentHp;
        await room.Engine.PlayCardAsync(owner, AddToHand<Task4ProbeAttack>(owner), target);
        Assert.Equal(hpBefore - 5, target.CurrentHp);

        hpBefore = target.CurrentHp;
        await room.Engine.PlayCardAsync(ally, AddToHand<Task4UnpoweredAttack>(ally), target);
        Assert.Equal(hpBefore - 5, target.CurrentHp);

        hpBefore = target.CurrentHp;
        await room.Engine.PlayCardAsync(ally, AddToHand<Task4ProbeAttack>(ally), target);
        Assert.Equal(hpBefore - expectedAllyDamage, target.CurrentHp);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Contains(knockdown, target.Powers);
        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, room.Engine.State.Enemies);
        Assert.DoesNotContain(knockdown, target.Powers);

        hpBefore = target.CurrentHp;
        await room.Engine.PlayCardAsync(ally, AddToHand<Task4ProbeAttack>(ally), target);
        Assert.Equal(hpBefore - 5, target.CurrentHp);
    }

    [Fact]
    public async Task Knockdown_SeparateApplicationsRemainInstancedAndMultiplyTogether()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            "task4-knockdown-instanced", 3, 1);
        Creature target = room.Engine.State.Enemies.Single();

        await room.Engine.PlayCardAsync(players[0], AddToHand<Knockdown>(players[0]), target);
        await room.Engine.PlayCardAsync(players[1], AddToHand<Knockdown>(players[1]), target);

        Assert.Equal(2, target.Powers.Count(power => power.GetType().Name == "KnockdownPower"));
        int hpBefore = target.CurrentHp;
        await room.Engine.PlayCardAsync(players[2], AddToHand<Task4ProbeAttack>(players[2]), target);
        Assert.Equal(hpBefore - 20, target.CurrentHp);
    }

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task MonarchsGaze_AppliesTemporaryStrengthLossForEveryDamageResult(
        bool upgraded,
        int expectedEnergySpent)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task4-monarchs-gaze-{upgraded}", 1, 1);
        Player owner = players[0];
        Creature target = room.Engine.State.Enemies.Single();
        MonarchsGaze card = AddToHand<MonarchsGaze>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }
        owner.PlayerCombatState!.Energy = 3;

        await room.Engine.PlayCardAsync(owner, card, target: null);
        Assert.Equal(3 - expectedEnergySpent, owner.PlayerCombatState.Energy);

        int hpBefore = target.CurrentHp;
        await room.Engine.PlayCardAsync(owner, AddToHand<Task4ThreeHitAttack>(owner), target);
        Assert.Equal(hpBefore - 12, target.CurrentHp);
        PowerModel strengthDown = Assert.Single(
            target.Powers,
            power => power.GetType().Name == "MonarchsGazeStrengthDownPower");
        Assert.Equal(3, strengthDown.Amount);
        Assert.Equal(-3, target.GetPower<StrengthPower>()!.Amount);

        int ownerHpBefore = owner.Creature.CurrentHp;
        await DamageCmd.Attack(10m).FromMonster(target.Monster!).Execute();
        Assert.Equal(ownerHpBefore - 7, owner.Creature.CurrentHp);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Enemy, room.Engine.State.Enemies);
        Assert.DoesNotContain(strengthDown, target.Powers);
    }

    [Theory]
    [InlineData(false, 11)]
    [InlineData(true, 15)]
    public async Task TagTeam_UpgradeChangesOnlyDamageAndEligibleAllyAttackReplaysOnce(
        bool upgraded,
        int expectedTagTeamDamage)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task4-tag-team-{upgraded}", 2, 2);
        Player owner = players[0];
        Player ally = players[1];
        Creature marked = room.Engine.State.Enemies[0];
        Creature other = room.Engine.State.Enemies[1];
        TagTeam card = AddToHand<TagTeam>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }
        int hpBefore = marked.CurrentHp;

        await room.Engine.PlayCardAsync(owner, card, marked);

        Assert.Equal(hpBefore - expectedTagTeamDamage, marked.CurrentHp);
        PowerModel marker = Assert.Single(
            marked.Powers,
            power => power.GetType().Name == "TagTeamPower");
        Assert.Equal(1, marker.Amount);

        hpBefore = marked.CurrentHp;
        await room.Engine.PlayCardAsync(owner, AddToHand<Task4ProbeAttack>(owner), marked);
        Assert.Equal(hpBefore - 5, marked.CurrentHp);
        Assert.Contains(marker, marked.Powers);

        int otherHpBefore = other.CurrentHp;
        await room.Engine.PlayCardAsync(ally, AddToHand<Task4ProbeAttack>(ally), other);
        Assert.Equal(otherHpBefore - 5, other.CurrentHp);
        Assert.Contains(marker, marked.Powers);

        Task4ReplayProbeAttack replay = AddToHand<Task4ReplayProbeAttack>(ally);
        hpBefore = marked.CurrentHp;
        await room.Engine.PlayCardAsync(ally, replay, marked);

        Assert.Equal(hpBefore - 10, marked.CurrentHp);
        Assert.Equal(2, replay.PlayExecutions);
        Assert.Same(marked, replay.FirstTarget);
        Assert.Same(marked, replay.SecondTarget);
        Assert.False(replay.SawTagTeamMarkerDuringPlay);
        Assert.DoesNotContain(marker, marked.Powers);

        hpBefore = marked.CurrentHp;
        await room.Engine.PlayCardAsync(ally, AddToHand<Task4ProbeAttack>(ally), marked);
        Assert.Equal(hpBefore - 5, marked.CurrentHp);
    }

    [Theory]
    [InlineData(false, 50)]
    [InlineData(true, 75)]
    public async Task TheGambit_KillsOnUnblockedPoweredHitWithoutSyntheticDamageResolution(
        bool upgraded,
        int expectedBlock)
    {
        var observer = new Task4DamageObserver();
        (Player[] players, CombatRoom room) = await CreateCombatAsync(
            $"task4-gambit-{upgraded}", 1, 1, observer);
        Player owner = players[0];
        Creature enemy = room.Engine.State.Enemies.Single();
        TheGambit card = AddToHand<TheGambit>(owner);
        if (upgraded)
        {
            card.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, card, target: null);
        Assert.Equal(expectedBlock, owner.Creature.Block);
        PowerModel gambit = Assert.Single(
            owner.Creature.Powers,
            power => power.GetType().Name == "TheGambitPower");
        Assert.Equal(1, gambit.Amount);

        observer.Results.Clear();
        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { owner.Creature },
            expectedBlock,
            ValueProp.Move,
            enemy,
            cardSource: null,
            cardPlay: null);
        Assert.True(owner.Creature.IsAlive);
        Assert.Contains(gambit, owner.Creature.Powers);
        Assert.Single(observer.Results);

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { owner.Creature },
            1m,
            ValueProp.Unpowered | ValueProp.Unblockable,
            enemy,
            cardSource: null,
            cardPlay: null);
        Assert.True(owner.Creature.IsAlive);
        Assert.Contains(gambit, owner.Creature.Powers);
        Assert.Equal(2, observer.Results.Count);

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { owner.Creature },
            1m,
            ValueProp.Move,
            enemy,
            cardSource: null,
            cardPlay: null);

        Assert.True(owner.Creature.IsDead);
        Assert.DoesNotContain(gambit, owner.Creature.Powers);
        Assert.Equal(3, observer.Results.Count);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player[] Players, CombatRoom Room)> CreateCombatAsync(
        string seed,
        int playerCount,
        int enemyCount,
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

        var room = new CombatRoom((Func<IReadOnlyList<MonsterModel>>)(() =>
            Enumerable.Range(0, enemyCount)
                .Select(_ => (MonsterModel)ModelDb.Monster<Task4HighHpMonster>().MutableClone())
                .ToArray()));
        if (observer is not null)
        {
            room.ConfigureObserver(observer);
        }
        await room.Enter(runState);
        return (players, room);
    }
}

file sealed class Task4ProbeAttack : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(5m).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}

file sealed class Task4UnpoweredAttack : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(5m)
            .Unpowered()
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
    }
}

file sealed class Task4ThreeHitAttack : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(4m)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .WithHitCount(3)
            .Execute();
    }
}

file sealed class Task4ReplayProbeAttack : CardModel
{
    public int PlayExecutions { get; private set; }
    public Creature? FirstTarget { get; private set; }
    public Creature? SecondTarget { get; private set; }
    public bool SawTagTeamMarkerDuringPlay { get; private set; }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        PlayExecutions++;
        if (PlayExecutions == 1)
        {
            FirstTarget = cardPlay.Target;
        }
        else if (PlayExecutions == 2)
        {
            SecondTarget = cardPlay.Target;
        }
        SawTagTeamMarkerDuringPlay |= cardPlay.Target.Powers.Any(
            power => power.GetType().Name == "TagTeamPower");
        await DamageCmd.Attack(5m).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}

file sealed class Task4HighHpMonster : MonsterModel
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

file sealed class Task4DamageObserver : ICombatObserver
{
    public List<DamageResult> Results { get; } = [];

    public void CombatStarted(CombatState state) { }
    public void PlayerTurnStarted(CombatState state) { }
    public void CardDrawn(CardModel card) { }
    public void CardPlayStarted(CardModel card, Creature? target) { }
    public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay) { }
    public void EnemyMoveStarted(Creature source, string moveId) { }
    public void EnemyMoveFinished(Creature source, string moveId) { }
    public void DamageResolved(Creature? dealer, DamageResult result) => Results.Add(result);
    public void PlayerTurnEnded(CombatState state) { }
    public void PotionUseStarted(PotionModel potion, Creature? target) { }
    public void PotionUseFinished(PotionModel potion, Creature? target) { }
}
