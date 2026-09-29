using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class OnKillCardTests : IDisposable
{
    public OnKillCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(KnockoutBlow), typeof(HandOfGreed), typeof(MinionPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task KnockoutBlow_Upgrade_KillsEnemy_AndGrantsFiveStars()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("knockout-blow");
        KnockoutBlow card = AddToHand<KnockoutBlow>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp - 1, Sts2Sim.Core.ValueProps.ValueProp.Move);
        int starsBefore = player.PlayerCombatState!.Stars;

        await card.PlayAsync(enemy);

        Assert.True(enemy.IsDead);
        Assert.Equal(starsBefore + 5, player.PlayerCombatState!.Stars);
    }

    [Fact]
    public async Task KnockoutBlow_DoesNotKill_NoStarsGranted()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("knockout-blow-no-kill");
        KnockoutBlow card = AddToHand<KnockoutBlow>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int starsBefore = player.PlayerCombatState!.Stars;

        await card.PlayAsync(enemy);

        Assert.False(enemy.IsDead);
        Assert.Equal(starsBefore, player.PlayerCombatState!.Stars);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandOfGreed_Upgrade_KillGrantsGoldOnlyWhenFatalAllowed(bool killedMinion)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"hand-of-greed-{killedMinion}");
        HandOfGreed card = AddToHand<HandOfGreed>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        enemy.LoseHpInternal(enemy.CurrentHp - 1, Sts2Sim.Core.ValueProps.ValueProp.Move);
        if (killedMinion)
            await PowerCmd.Apply<MinionPower>(room.Engine.State, enemy, 1m, enemy, null);
        int goldBefore = player.Gold;

        await card.PlayAsync(enemy);

        Assert.True(enemy.IsDead);
        Assert.Equal(goldBefore + (killedMinion ? 0 : 25), player.Gold);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : Sts2Sim.Core.Models.CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, Sts2Sim.Core.Entities.Cards.PileType.Hand);
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
