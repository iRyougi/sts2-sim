using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class BlackHoleAndChildOfTheStarsTests : IDisposable
{
    private sealed class TwoStarSkill : CardModel
    {
        public int EnemyHpDuringPlay { get; private set; }

        public override CardType Type => CardType.Skill;
        public override CardRarity Rarity => CardRarity.Basic;
        public override TargetType TargetType => TargetType.Self;
        protected override int CanonicalEnergyCost => 0;
        protected override int CanonicalStarCost => 2;

        protected override Task OnPlay(CardPlay cardPlay)
        {
            EnemyHpDuringPlay = CombatState!.Enemies.Single().CurrentHp;
            return Task.CompletedTask;
        }
    }

    public BlackHoleAndChildOfTheStarsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(BlackHole), typeof(ChildOfTheStars), typeof(BlackHolePower), typeof(ChildOfTheStarsPower),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 3, 2)]
    [InlineData(true, 4, 3)]
    public async Task PowerCards_ApplyTheirBaseAndUpgradedAmounts(bool upgraded, int expectedBlackHole, int expectedChild)
    {
        (Player player, _) = await CreateCombatAsync($"power-amount-{upgraded}");
        BlackHole blackHole = AddToHand<BlackHole>(player);
        ChildOfTheStars child = AddToHand<ChildOfTheStars>(player);
        if (upgraded)
        {
            blackHole.Upgrade();
            child.Upgrade();
        }

        await blackHole.PlayAsync(target: null);
        await child.PlayAsync(target: null);

        Assert.Equal(expectedBlackHole, player.Creature.GetPower<BlackHolePower>()!.Amount);
        Assert.Equal(expectedChild, player.Creature.GetPower<ChildOfTheStarsPower>()!.Amount);
    }

    [Fact]
    public async Task BlackHole_DamagesEveryEnemyOnceForAnyPositiveStarsGain()
    {
        (Player player, CombatState combatState) = CreateTwoEnemyCombatState("black-hole-positive-stars");
        await AddToHand<BlackHole>(player).PlayAsync(target: null);
        int[] hpBefore = combatState.Enemies.Select(enemy => enemy.CurrentHp).ToArray();

        await PlayerCmd.GainStars(3, player);

        Assert.Equal(hpBefore[0] - 3, combatState.Enemies[0].CurrentHp);
        Assert.Equal(hpBefore[1] - 3, combatState.Enemies[1].CurrentHp);
    }

    [Fact]
    public async Task BlackHole_DamagesOnceAfterOwnerSpendsMultipleStars()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("black-hole-stars-spent");
        await AddToHand<BlackHole>(player).PlayAsync(target: null);
        player.PlayerCombatState!.GainStars(2);
        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;

        TwoStarSkill spentCard = AddToHand((TwoStarSkill)new TwoStarSkill().MutableClone(), player);
        await spentCard.PlayAsync(target: null);

        Assert.Equal(hpBefore, spentCard.EnemyHpDuringPlay);
        Assert.Equal(hpBefore - 3, room.Engine.State.Enemies.Single().CurrentHp);
    }

    [Theory]
    [InlineData(false, 4)]
    [InlineData(true, 6)]
    public async Task ChildOfTheStars_GainsBlockForExactStarsSpent(bool upgraded, int expectedBlock)
    {
        (Player player, _) = await CreateCombatAsync($"child-stars-spent-{upgraded}");
        ChildOfTheStars child = AddToHand<ChildOfTheStars>(player);
        if (upgraded)
        {
            child.Upgrade();
        }
        await child.PlayAsync(target: null);
        player.PlayerCombatState!.GainStars(2);

        await AddToHand((TwoStarSkill)new TwoStarSkill().MutableClone(), player).PlayAsync(target: null);

        Assert.Equal(expectedBlock, player.Creature.Block);
    }

    [Fact]
    public async Task ReapplyingBothPowers_UsesNormalCounterStacking()
    {
        (Player player, _) = await CreateCombatAsync("power-normal-stacking");

        await AddToHand<BlackHole>(player).PlayAsync(target: null);
        await AddToHand<BlackHole>(player).PlayAsync(target: null);
        await AddToHand<ChildOfTheStars>(player).PlayAsync(target: null);
        await AddToHand<ChildOfTheStars>(player).PlayAsync(target: null);

        Assert.Equal(6, player.Creature.GetPower<BlackHolePower>()!.Amount);
        Assert.Equal(4, player.Creature.GetPower<ChildOfTheStarsPower>()!.Amount);
    }

    [Fact]
    public async Task BlackHole_IgnoresZeroGainAndAnotherPlayer()
    {
        (Player owner, Player other, CombatRoom room) = await CreateTwoPlayerCombatAsync("black-hole-ignore");
        await AddToHand<BlackHole>(owner).PlayAsync(target: null);
        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;

        await PlayerCmd.GainStars(0, owner);
        await PlayerCmd.GainStars(2, other);

        Assert.Equal(hpBefore, room.Engine.State.Enemies.Single().CurrentHp);
    }

    [Fact]
    public async Task BlackHole_IgnoresStarsSpentByAnotherPlayer()
    {
        (Player owner, Player other, CombatRoom room) = await CreateTwoPlayerCombatAsync("black-hole-other-spender");
        await AddToHand<BlackHole>(owner).PlayAsync(target: null);
        other.PlayerCombatState!.GainStars(2);
        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;

        await AddToHand((TwoStarSkill)new TwoStarSkill().MutableClone(), other).PlayAsync(target: null);

        Assert.Equal(hpBefore, room.Engine.State.Enemies.Single().CurrentHp);
    }

    [Fact]
    public async Task BlackHole_DamagesOnlyAfterTheLastCardInSeries()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("black-hole-last-in-series");
        await AddToHand<BlackHole>(player).PlayAsync(target: null);
        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;
        CardModel card = (TwoStarSkill)new TwoStarSkill().MutableClone();
        card.AssignOwner(player);
        var nonFinalPlay = new CardPlay
        {
            Card = card,
            Player = player,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo(0, 0, 2, 0),
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 2,
        };

        await Hook.AfterCardPlayed(room.Engine.State, nonFinalPlay);

        Assert.Equal(hpBefore, room.Engine.State.Enemies.Single().CurrentHp);
    }

    [Fact]
    public async Task ChildOfTheStars_IgnoresStarsSpentByAnotherPlayer()
    {
        (Player owner, Player other, _) = await CreateTwoPlayerCombatAsync("child-other-player");
        await AddToHand<ChildOfTheStars>(owner).PlayAsync(target: null);
        other.PlayerCombatState!.GainStars(2);

        await AddToHand((TwoStarSkill)new TwoStarSkill().MutableClone(), other).PlayAsync(target: null);

        Assert.Equal(0, owner.Creature.Block);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel => AddToHand((TCard)ModelDb.Card<TCard>().MutableClone(), player);

    private static TCard AddToHand<TCard>(TCard card, Player player)
        where TCard : CardModel
    {
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        player.PlayerCombatState.Energy = 10;
        return card;
    }

    private static (Player player, CombatState combatState) CreateTwoEnemyCombatState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        combatState.AddMonster((WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(), CombatSide.Enemy);
        combatState.AddMonster((WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(), CombatSide.Enemy);
        return (player, combatState);
    }

    private static async Task<(Player owner, Player other, CombatRoom room)> CreateTwoPlayerCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(other);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (owner, other, room);
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
