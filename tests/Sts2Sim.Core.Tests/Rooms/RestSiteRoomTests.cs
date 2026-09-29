using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public class RestSiteRoomTests
{
    static RestSiteRoomTests()
    {
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    [Fact]
    public async Task ResolveAsync_Heal_HealsPlayerBy30PercentMaxHp()
    {
        var runState = new RunState("rest-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.Creature.LoseHpInternal(player.Creature.MaxHp - 1, default(ValueProp));
        int hpBefore = player.Creature.CurrentHp;

        var room = new RestSiteRoom();
        await room.EnterInternal(runState);
        await room.ResolveAsync(player, new RestSiteDecision.Heal());

        int expectedHeal = (int)(player.Creature.MaxHp * 0.3m);
        Assert.Equal(Math.Min(hpBefore + expectedHeal, player.Creature.MaxHp), player.Creature.CurrentHp);
    }
}
