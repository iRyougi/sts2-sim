using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Commands;

file sealed class DoubleMimicRestSiteHealRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount) =>
        ReferenceEquals(creature, Owner.Creature) ? amount * 2m : amount;
}
[Collection("ModelDb")]
public class PlayerCmdTests : IDisposable
{
    public PlayerCmdTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight), typeof(DoubleMimicRestSiteHealRelic) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task GainGold_AddsAmountToPlayerGold()
    {
        var runState = new RunState("player-cmd-gold", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        int before = player.Gold;

        await PlayerCmd.GainGold(25m, player);

        Assert.Equal(before + 25, player.Gold);
    }

    [Fact]
    public async Task LoseGold_ClampsAtZero()
    {
        var runState = new RunState("player-cmd-lose-gold", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        await PlayerCmd.LoseGold(player.Gold + 999, player);

        Assert.Equal(0, player.Gold);
    }
    [Fact]
    public async Task MimicRestSiteHeal_HealsThirtyPercentOfMaximumHp()
    {
        var runState = new RunState("player-cmd-mimic-rest", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        await CreatureCmd.LoseHp(runState, player.Creature, 50m, ValueProp.Unblockable);
        int hpBefore = player.Creature.CurrentHp;

        await PlayerCmd.MimicRestSiteHeal(player, playSfx: false);

        Assert.Equal(hpBefore + (int)(player.Creature.MaxHp * 0.3m), player.Creature.CurrentHp);
    }

    [Fact]
    public async Task MimicRestSiteHeal_FoldsModifyRestSiteHealAmountHooks()
    {
        var runState = new RunState("player-cmd-mimic-rest-hook", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        await RelicCmd.Obtain(ModelDb.Relic<DoubleMimicRestSiteHealRelic>(), player);
        await CreatureCmd.LoseHp(runState, player.Creature, 60m, ValueProp.Unblockable);
        int hpBefore = player.Creature.CurrentHp;

        await PlayerCmd.MimicRestSiteHeal(player, playSfx: false);

        Assert.Equal(hpBefore + (int)(player.Creature.MaxHp * 0.6m), player.Creature.CurrentHp);
    }
}
