namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class MonsterStatusCreatorTests : IDisposable
{
    public MonsterStatusCreatorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("Chomper", "SCREECH_MOVE", 0, 3)]
    [InlineData("Noisebot", "NOISE_MOVE", 1, 1)]
    [InlineData("TheInsatiable", "LIQUIFY_GROUND_MOVE", 3, 3)]
    public async Task EnemyStatusCardsDoNotTriggerPlayerGenerationRelic(
        string monsterName, string expectedMove, int expectedDraw, int expectedDiscard)
    {
        string seed = $"09-c1-202-{monsterName}";
        var run = new RunState(seed, new Overgrowth(), 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        await RelicCmd.Obtain(ModelDb.Relic<Regalite>(), player);

        MonsterModel monster = monsterName switch
        {
            "Chomper" => (Chomper)ModelDb.Monster<Chomper>().MutableClone(),
            "Noisebot" => (Noisebot)ModelDb.Monster<Noisebot>().MutableClone(),
            "TheInsatiable" => (TheInsatiable)ModelDb.Monster<TheInsatiable>().MutableClone(),
            _ => throw new ArgumentOutOfRangeException(nameof(monsterName)),
        };
        if (monster is Chomper chomper)
        {
            chomper.ScreamFirst = true;
        }

        var room = new CombatRoom(() => monster);
        run.PushRoom(room);
        await room.Enter(run);
        Assert.Equal(expectedMove, monster.NextMove?.StateId);

        await monster.PerformMove();

        var piles = player.PlayerCombatState!;
        int drawCount = monster is TheInsatiable
            ? piles.DrawPile.Cards.OfType<FranticEscape>().Count()
            : piles.DrawPile.Cards.OfType<Dazed>().Count();
        int discardCount = monster is TheInsatiable
            ? piles.DiscardPile.Cards.OfType<FranticEscape>().Count()
            : piles.DiscardPile.Cards.OfType<Dazed>().Count();
        int block = player.Creature.Block;
        Assert.True(drawCount == expectedDraw && discardCount == expectedDiscard && block == 0,
            $"seed={seed}; monster={monsterName}; draw={drawCount}, discard={discardCount}, block={block}; " +
            $"expected {expectedDraw},{expectedDiscard},0 for null creator");
    }
}
