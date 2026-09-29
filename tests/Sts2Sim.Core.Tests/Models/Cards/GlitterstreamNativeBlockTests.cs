using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
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
public sealed class GlitterstreamNativeBlockTests : IDisposable
{
    public GlitterstreamNativeBlockTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WanderingGrunt), typeof(SovereignBlade), typeof(DivineRight),
            typeof(Glitterstream), typeof(DexterityPower), typeof(BlockNextTurnPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 13, 7)]
    [InlineData(true, 15, 9)]
    public async Task Glitterstream_ModifiesNextTurnBlockBeforeGainingCurrentBlock(
        bool upgraded, int currentBlock, int nextTurnBlock)
    {
        var runState = new RunState($"glitterstream-native-{upgraded}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        await PowerCmd.Apply<DexterityPower>(room.Engine.State, player.Creature, 2m, player.Creature, null);

        var card = (Glitterstream)ModelDb.Card<Glitterstream>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        if (upgraded)
        {
            card.Upgrade();
        }

        await card.PlayAsync(target: null);

        Assert.Equal(currentBlock, player.Creature.Block);
        Assert.Equal(nextTurnBlock, player.Creature.Powers.OfType<BlockNextTurnPower>().Single().Amount);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(nextTurnBlock, player.Creature.Block);
    }
}
