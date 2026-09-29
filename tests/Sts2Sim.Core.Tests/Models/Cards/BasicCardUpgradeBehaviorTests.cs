using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
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

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class BasicCardUpgradeBehaviorTests : IDisposable
{
    public BasicCardUpgradeBehaviorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task StrikeRegent_Upgrade_DealsNineDamage()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("strike-upgrade");
        StrikeRegent card = AddToHand<StrikeRegent>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int startingHp = enemy.CurrentHp;

        card.Upgrade();
        await card.PlayAsync(enemy);

        Assert.Equal(startingHp - 9, enemy.CurrentHp);
    }

    [Fact]
    public async Task DefendRegent_Upgrade_GainsEightBlock()
    {
        (Player player, _) = await CreateCombatAsync("defend-upgrade");
        DefendRegent card = AddToHand<DefendRegent>(player);

        card.Upgrade();
        await card.PlayAsync(target: null);

        Assert.Equal(8, player.Creature.Block);
    }

    [Fact]
    public async Task FallingStar_Upgrade_DealsTwelveDamage()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("falling-star-upgrade");
        player.PlayerCombatState!.GainStars(2m);
        FallingStar card = AddToHand<FallingStar>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int startingHp = enemy.CurrentHp;

        card.Upgrade();
        await card.PlayAsync(enemy);

        Assert.Equal(startingHp - 12, enemy.CurrentHp);
    }

    [Fact]
    public async Task Venerate_Upgrade_GrantsThreeStars()
    {
        (Player player, _) = await CreateCombatAsync("venerate-upgrade");
        Venerate card = AddToHand<Venerate>(player);
        int startingStars = player.PlayerCombatState!.Stars;

        card.Upgrade();
        await card.PlayAsync(target: null);

        Assert.Equal(startingStars + 3, player.PlayerCombatState.Stars);
    }

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
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        player.PlayerCombatState!.Energy = 3;
        return (player, room);
    }
}
