using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Entities.Players;

[Collection("ModelDb")]
public sealed class PlayerInnateDrawTests : IDisposable
{
    public PlayerInnateDrawTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
            typeof(NormalKeywordTestCard), typeof(InnateKeywordTestCard),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task StartCombat_DrawsInnateCardForEverySeed()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            var runState = new RunState($"innate-{seed}", new Overgrowth());
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            ReplaceDeckWithInnateAndNormalCards(player);
            var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());

            await room.Enter(runState);

            Assert.Single(player.PlayerCombatState!.Hand.Cards.OfType<InnateKeywordTestCard>());
        }
    }

    private static void ReplaceDeckWithInnateAndNormalCards(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToList())
        {
            player.Deck.RemoveInternal(card);
        }

        AddToDeck<InnateKeywordTestCard>(player);
        for (int i = 0; i < 9; i++)
        {
            AddToDeck<NormalKeywordTestCard>(player);
        }
    }

    private static void AddToDeck<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.Deck.AddInternal(card);
    }
}
