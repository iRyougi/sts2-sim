using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Potions;

[Collection("ModelDb")]
public class StrengthPotionTests : IDisposable
{
    public StrengthPotionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight), typeof(StrengthPotion), typeof(StrengthPower), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Use_GrantsTwoStrengthToTarget_AndConsumesPotion()
    {
        var runState = new RunState("strength-potion-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);

        var potion = (StrengthPotion)ModelDb.Potion<StrengthPotion>().MutableClone();
        player.AddPotionInternal(potion);

        await PotionCmd.Use(potion, player, player.Creature);

        var strength = player.Creature.Powers.OfType<StrengthPower>().Single();
        Assert.Equal(2, strength.Amount);
        Assert.DoesNotContain(potion, player.PotionSlots);
    }
}
