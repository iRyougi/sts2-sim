using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class HiddenGemAndReplayTests : IDisposable
{
    public HiddenGemAndReplayTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(HiddenGem),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task HiddenGem_GrantsReplayToOneEligibleDrawPileCard()
    {
        (Player player, _) = await CreateCombatAsync("hidden-gem");
        foreach (CardModel c in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(c, PileType.Draw, CardPilePosition.Top);
        }

        for (int i = 0; i < 5; i++)
        {
            CardPileCmd.Add(AddTo<DefendRegent>(player), PileType.Draw, CardPilePosition.Top);
        }

        HiddenGem card = AddToHand<HiddenGem>(player);

        await card.PlayAsync(target: null);

        int totalReplay = player.PlayerCombatState!.DrawPile.Cards.Sum(c => c.BaseReplayCount);
        Assert.Equal(2, totalReplay);
        Assert.Equal(1, player.PlayerCombatState!.DrawPile.Cards.Count(c => c.BaseReplayCount > 0));
    }

    [Fact]
    public async Task PlayAsync_WithBaseReplayCount_RunsOnPlayExtraTimes()
    {
        (Player player, _) = await CreateCombatAsync("replay-loop");
        DefendRegent card = AddToHand<DefendRegent>(player);
        card.BaseReplayCount = 2;
        int blockBefore = player.Creature.Block;

        await card.PlayAsync(target: null);

        Assert.Equal(blockBefore + 5 * 3, player.Creature.Block);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel =>
        AddTo<TCard>(player, PileType.Hand);

    private static TCard AddTo<TCard>(Player player, PileType pileType = PileType.None)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        if (pileType != PileType.None)
        {
            CardPileCmd.Add(card, pileType);
        }

        return card;
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
