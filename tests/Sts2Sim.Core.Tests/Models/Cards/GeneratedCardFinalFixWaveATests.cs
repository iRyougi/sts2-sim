using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedCardFinalFixWaveATests : IDisposable
{
    public GeneratedCardFinalFixWaveATests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 11)]
    public async Task DyingStar_AppliesTemporaryStrengthToPreDamageEnemySnapshot(
        bool upgraded,
        int expectedAmount)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"dying-star-snapshot-{upgraded}");
        Creature survivor = room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            CombatSide.Enemy);
        Creature lethalTarget = room.Engine.State.HittableEnemies.First(enemy => enemy != survivor);
        lethalTarget.LoseHpInternal(lethalTarget.CurrentHp - 1, ValueProp.Unblockable);
        int survivorHpBefore = survivor.CurrentHp;

        DyingStar card = AddToHand<DyingStar>(player);
        if (upgraded)
        {
            card.Upgrade();
        }
        player.PlayerCombatState!.GainStars(3);

        await card.PlayAsync(target: null);

        Assert.True(lethalTarget.IsDead);
        Assert.Equal(survivorHpBefore - expectedAmount, survivor.CurrentHp);
        DyingStarPower? lethalPower = lethalTarget.GetPower<DyingStarPower>();
        Assert.Null(lethalTarget.CombatState);
        Assert.Null(lethalPower); // Source CanReceivePowers rejects the detached target.
        Assert.Equal(expectedAmount, survivor.GetPower<DyingStarPower>()!.Amount);

        await Sts2Sim.Core.Hooks.Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);

        Assert.Null(lethalTarget.GetPower<DyingStarPower>());
        Assert.Null(survivor.GetPower<DyingStarPower>());
    }

    [Fact]
    public async Task BigBang_ResolvesDrawThenStarsThenEnergyThenForge()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("big-bang-order");
        var probe = (BigBangOrderProbe)new BigBangOrderProbe().MutableClone();
        probe.ApplyInternal(player.Creature, 1m);
        SovereignBlade blade = AddToHand<SovereignBlade>(player);
        BigBang card = AddToHand<BigBang>(player);
        card.Upgrade();
        Assert.True(card.HasKeyword(CardKeyword.Innate));

        player.PlayerCombatState!.Energy = 4;
        int energyBefore = player.PlayerCombatState.Energy;
        int starsBefore = player.PlayerCombatState.Stars;
        int handCountBefore = player.PlayerCombatState.Hand.Cards.Count;
        int enemyHpBefore = room.Engine.State.HittableEnemies.Single().CurrentHp;

        await card.PlayAsync(target: null);

        Assert.Equal(handCountBefore, probe.HandCountAtStarsGained);
        Assert.Equal(energyBefore, probe.EnergyAtStarsGained);
        Assert.Equal(starsBefore + 1, probe.StarsAtForge);
        Assert.Equal(energyBefore + 1, probe.EnergyAtForge);
        Assert.Equal(handCountBefore, probe.HandCountAtForge);
        Assert.Equal(energyBefore + 1, player.PlayerCombatState.Energy);
        Assert.Equal(starsBefore + 1, player.PlayerCombatState.Stars);
        Assert.Equal(15m, GetBladeDamage(blade));
        Assert.Equal(enemyHpBefore, room.Engine.State.HittableEnemies.Single().CurrentHp);
    }

    private static decimal GetBladeDamage(SovereignBlade blade) =>
        (decimal)typeof(SovereignBlade)
            .GetField("_damage", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(blade)!;

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

file sealed class BigBangOrderProbe : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public int HandCountAtStarsGained { get; private set; } = -1;

    public int EnergyAtStarsGained { get; private set; } = -1;

    public int StarsAtForge { get; private set; } = -1;

    public int EnergyAtForge { get; private set; } = -1;

    public int HandCountAtForge { get; private set; } = -1;

    public override Task AfterStarsGained(int amount, Player gainer)
    {
        HandCountAtStarsGained = gainer.PlayerCombatState!.Hand.Cards.Count;
        EnergyAtStarsGained = gainer.PlayerCombatState.Energy;
        return Task.CompletedTask;
    }

    public override Task AfterForge(decimal amount, Player forger, AbstractModel? source)
    {
        StarsAtForge = forger.PlayerCombatState!.Stars;
        EnergyAtForge = forger.PlayerCombatState.Energy;
        HandCountAtForge = forger.PlayerCombatState.Hand.Cards.Count;
        return Task.CompletedTask;
    }
}
