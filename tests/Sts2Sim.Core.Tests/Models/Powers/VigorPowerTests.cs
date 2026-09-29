using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class VigorPowerTests : IDisposable
{
    public VigorPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(VigorPower), typeof(DivineRight),
            typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task PoweredAttack_GainsVigorDamageOnceThenConsumesVigor()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("vigor-powered");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<VigorPower>(room.Engine.State, player.Creature, 3m, player.Creature, null);
        StrikeRegent first = AddToHand(player);
        StrikeRegent second = AddToHand(player);
        int hpBefore = enemy.CurrentHp;

        await first.PlayAsync(enemy);
        await second.PlayAsync(enemy);

        Assert.Equal(hpBefore - 15, enemy.CurrentHp);
        Assert.DoesNotContain(player.Creature.Powers, power => power is VigorPower);
    }

    [Fact]
    public async Task UnpoweredAttack_DoesNotGainOrConsumeVigor()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("vigor-unpowered");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        VigorPower vigor = Assert.IsType<VigorPower>(
            await PowerCmd.Apply<VigorPower>(room.Engine.State, player.Creature, 3m, player.Creature, null));
        StrikeRegent source = AddToHand(player);
        int hpBefore = enemy.CurrentHp;

        await DamageCmd.Attack(6m).FromCard(source, cardPlay: null).Unpowered().Targeting(enemy).Execute();

        Assert.Equal(hpBefore - 6, enemy.CurrentHp);
        Assert.Equal(3, vigor.Amount);
        Assert.Contains(vigor, player.Creature.Powers);
    }

    private static StrikeRegent AddToHand(Player player)
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        player.PlayerCombatState.Energy = 3;
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
