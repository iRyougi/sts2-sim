namespace Sts2Sim.Core.Tests.Models.Powers;

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
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class SlowPowerTests : IDisposable
{
    public SlowPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight), typeof(WanderingGrunt), typeof(SovereignBlade), typeof(SlowPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task CombatEnginePlay_FirstAttackIsUnmodified_SecondGetsTenPercentForPriorCard()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("slow-card-timing");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        SlowPower slow = (await PowerCmd.Apply<SlowPower>(
            room.Engine.State,
            enemy,
            2m,
            applier: enemy,
            cardSource: null))!;
        SovereignBlade first = AddToHand(player);
        SovereignBlade second = AddToHand(player);
        player.PlayerCombatState!.Energy = 10;
        int hpBefore = enemy.CurrentHp;

        await room.Engine.PlayCardAsync(player, first, enemy);
        Assert.Equal(hpBefore - 10, enemy.CurrentHp);

        await room.Engine.PlayCardAsync(player, second, enemy);
        Assert.Equal(hpBefore - 21, enemy.CurrentHp);
        Assert.Equal(2, slow.Amount);
    }

    [Fact]
    public async Task OwnersSideTurnStart_ThroughHook_ResetsAccumulatedCardCount()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("slow-reset");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<SlowPower>(room.Engine.State, enemy, 1m, enemy, cardSource: null);
        SovereignBlade first = AddToHand(player);
        player.PlayerCombatState!.Energy = 10;
        await room.Engine.PlayCardAsync(player, first, enemy);

        await Hook.AfterSideTurnStart(room.Engine.State, CombatSide.Enemy, new[] { enemy });
        int hpBefore = enemy.CurrentHp;
        SovereignBlade afterReset = AddToHand(player);
        await room.Engine.PlayCardAsync(player, afterReset, enemy);

        Assert.Equal(hpBefore - 10, enemy.CurrentHp);
    }

    [Fact]
    public async Task UnpoweredDamage_ThroughCreatureCmd_IsNotAmplifiedAfterCardPlayed()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("slow-unpowered");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<SlowPower>(room.Engine.State, enemy, 1m, enemy, cardSource: null);
        SovereignBlade card = AddToHand(player);
        player.PlayerCombatState!.Energy = 10;
        await room.Engine.PlayCardAsync(player, card, enemy);
        int hpBefore = enemy.CurrentHp;

        await CreatureCmd.Damage(
            room.Engine.State,
            new[] { enemy },
            10m,
            ValueProp.Unpowered,
            dealer: player.Creature,
            cardSource: null,
            cardPlay: null);

        Assert.Equal(hpBefore - 10, enemy.CurrentHp);
    }

    private static SovereignBlade AddToHand(Player player)
    {
        var card = (SovereignBlade)ModelDb.Card<SovereignBlade>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
