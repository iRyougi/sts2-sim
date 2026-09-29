namespace Sts2Sim.Core.Tests.Models.Cards;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class FallingStarAndVenerateTests : IDisposable
{
    public FallingStarAndVenerateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task FallingStar_DealsEightDamage_SpendsTwoStars_AndAppliesAttributedDebuffs()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("falling-star-a");
        player.PlayerCombatState!.Energy = 1;
        Assert.Equal(3, player.PlayerCombatState.Stars);
        player.PlayerCombatState.GainStars(2m);
        FallingStar fallingStar = (FallingStar)ModelDb.Card<FallingStar>().MutableClone();
        fallingStar.AssignOwner(player);
        player.PlayerCombatState.Hand.AddInternal(fallingStar);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int startingHp = enemy.CurrentHp;

        Assert.True(fallingStar.CanPlay(out UnplayableReason reason));
        Assert.Equal(UnplayableReason.None, reason);
        await fallingStar.PlayAsync(enemy);

        Assert.Equal(startingHp - 8, enemy.CurrentHp);
        Assert.Equal(1, player.PlayerCombatState.Energy);
        Assert.Equal(3, player.PlayerCombatState.Stars);
        WeakPower weak = enemy.Powers.OfType<WeakPower>().Single();
        VulnerablePower vulnerable = enemy.Powers.OfType<VulnerablePower>().Single();
        Assert.Equal(1m, weak.Amount);
        Assert.Equal(1m, vulnerable.Amount);
        Assert.Same(player.Creature, weak.Applier);
        Assert.Same(player.Creature, vulnerable.Applier);
    }

    [Fact]
    public async Task FallingStar_RequiresAnEnemyTarget()
    {
        (Player player, _) = await CreateCombatAsync("falling-star-b");
        player.PlayerCombatState!.GainStars(2m);
        FallingStar fallingStar = (FallingStar)ModelDb.Card<FallingStar>().MutableClone();
        fallingStar.AssignOwner(player);
        player.PlayerCombatState.Hand.AddInternal(fallingStar);

        await Assert.ThrowsAsync<ArgumentNullException>(() => fallingStar.PlayAsync(target: null));
    }

    [Fact]
    public async Task Venerate_CostsOneEnergy_AndGrantsTwoStars()
    {
        (Player player, _) = await CreateCombatAsync("venerate-a");
        player.PlayerCombatState!.Energy = 1;
        Assert.Equal(3, player.PlayerCombatState.Stars);
        Venerate venerate = (Venerate)ModelDb.Card<Venerate>().MutableClone();
        venerate.AssignOwner(player);
        player.PlayerCombatState.Hand.AddInternal(venerate);

        Assert.True(venerate.CanPlay(out UnplayableReason reason));
        Assert.Equal(UnplayableReason.None, reason);
        CardPlay? play = await venerate.PlayWithResultAsync(target: null);

        Assert.NotNull(play);
        Assert.Equal(new ResourceInfo(1, 1, 0, 0), play.Resources);
        Assert.Equal(0, player.PlayerCombatState.Energy);
        Assert.Equal(5, player.PlayerCombatState.Stars);
    }

    [Fact]
    public void NewCardModels_AreCanonical_AndPlayableInstancesAreMutableClones()
    {
        FallingStar fallingStar = ModelDb.Card<FallingStar>();
        Venerate venerate = ModelDb.Card<Venerate>();

        Assert.True(fallingStar.IsCanonical);
        Assert.True(venerate.IsCanonical);
        Assert.False(((FallingStar)fallingStar.MutableClone()).IsCanonical);
        Assert.False(((Venerate)venerate.MutableClone()).IsCanonical);
        Assert.Equal(CardType.Attack, fallingStar.Type);
        Assert.Equal(CardRarity.Basic, fallingStar.Rarity);
        Assert.Equal(TargetType.AnyEnemy, fallingStar.TargetType);
        Assert.Equal(0, fallingStar.EnergyCost);
        Assert.Equal(2, fallingStar.StarCost);
        Assert.Equal(CardType.Skill, venerate.Type);
        Assert.Equal(CardRarity.Basic, venerate.Rarity);
        Assert.Equal(TargetType.Self, venerate.TargetType);
        Assert.Equal(1, venerate.EnergyCost);
        Assert.Equal(-1, venerate.StarCost);
        Assert.False(venerate.HasStarCost);
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
