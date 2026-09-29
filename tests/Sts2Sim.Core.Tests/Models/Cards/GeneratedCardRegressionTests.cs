using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
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

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedCardRegressionTests : IDisposable
{
    public GeneratedCardRegressionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(StrengthPower), typeof(VigorPower), typeof(CrushUnderPower),
            typeof(DivineRight), typeof(WanderingGrunt),
            typeof(CrushUnder), typeof(Omnislice),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task CrushUnder_ReducesStrengthOfEveryEnemy()
    {
        (Player player, CombatRoom room) = await CreateTwoEnemyCombatAsync("crush-under");
        CrushUnder card = AddToHand<CrushUnder>(player);

        await card.PlayAsync(target: null);

        Assert.All(room.Engine.State.Enemies, enemy =>
            Assert.Equal(-1, enemy.Powers.OfType<StrengthPower>().Single().Amount));
        Assert.DoesNotContain(player.Creature.Powers, power => power is StrengthPower);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);

        Assert.All(room.Engine.State.Enemies, enemy =>
            Assert.DoesNotContain(enemy.Powers, power => power is StrengthPower));
    }

    [Fact]
    public async Task Omnislice_SplashesResolvedDamageToOtherTargetTeammates()
    {
        (Player player, CombatRoom room) = await CreateTwoEnemyCombatAsync("omnislice");
        Omnislice card = AddToHand<Omnislice>(player);
        Creature target = room.Engine.State.Enemies[0];
        Creature teammate = room.Engine.State.Enemies[1];
        int targetHpBefore = target.CurrentHp;
        int teammateHpBefore = teammate.CurrentHp;

        await card.PlayAsync(target);

        Assert.Equal(targetHpBefore - 8, target.CurrentHp);
        Assert.Equal(teammateHpBefore - 8, teammate.CurrentHp);
        Assert.Empty(target.Powers);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateTwoEnemyCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            CombatSide.Enemy);
        return (player, room);
    }
}
