using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Players;

[Collection("ModelDb")]
public class PlayerOddsWiringTests : IDisposable
{
    public PlayerOddsWiringTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void NewRun_HasNonNullPlayerRngAndOdds()
    {
        var runState = new RunState("player-odds-wiring", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);

        Assert.NotNull(player.PlayerRng);
        Assert.NotNull(player.Odds);
        Assert.NotNull(player.Odds.CardRarity);
        Assert.NotNull(player.Odds.PotionReward);
    }

    [Fact]
    public void RunState_DefaultsToAscensionZero()
    {
        var runState = new RunState("player-odds-wiring-ascension", new Overgrowth());

        Assert.False(runState.Ascension.HasLevel(AscensionLevel.Scarcity));
    }
}
