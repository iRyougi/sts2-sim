using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

[Collection("ModelDb")]
public sealed class RestSiteOpenDecisionTests : IDisposable
{
    public RestSiteOpenDecisionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("heal", "heal", 23, 75, 10, 0, 0)]
    [InlineData("smith", "smith:0:CARD.STRIKE_REGENT", 1, 75, 10, 1, 0)]
    [InlineData("hatch", "hatch", 1, 75, 11, 0, 1)]
    [InlineData("cook", "cook", 6, 80, 8, 0, 0)]
    public async Task ExternalDecision_PreservesExistingEffectsAndParticipatesInSelection(
        string optionId, string label, int hp, int maxHp, int deckCount, int upgrades, int pets)
    {
        var run = new RunState("open-rest-" + optionId, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        player.Creature.LoseHpInternal(74, default);
        CardModel firstCard = player.Deck.Cards[0];
        CardModel penultimateCard = player.Deck.Cards[8];
        CardModel lastCard = player.Deck.Cards[9];
        if (optionId == "hatch")
        {
            var egg = (ByrdonisEgg)ModelDb.Card<ByrdonisEgg>().MutableClone();
            egg.AssignOwner(player);
            player.Deck.AddInternal(egg);
        }

        RestSiteDecision[] builtIns =
        [
            new RestSiteDecision.Heal(), new RestSiteDecision.Smith(firstCard),
            new RestSiteDecision.Hatch(), new RestSiteDecision.Cook(),
        ];
        RestSiteDecision decision = builtIns.Single(candidate => candidate.OptionId == optionId);
        var extension = new ForwardingDecision(decision);

        Assert.Equal(label, RestSiteDecisionCandidates.Build(player, [decision]).Single().Label);
        Assert.Equal(
            optionId == "cook" ? ActionSpaceLayout.CookRestSiteIndex : ActionSpaceLayout.RestSiteIndex(0),
            RestSiteDecisionCandidates.Build(player, [decision]).Single().SlotIndex);
        Assert.Equal("content:" + label,
            RestSiteDecisionCandidates.Build(player, [extension]).Single().Label);
        Assert.Same(extension, RestSiteDecisionPolicy.ChooseDefault([.. builtIns, extension]));
        Assert.Same(extension, RestSiteDecisionPolicy.ChooseDefault([extension, new ForwardingDecision(decision)]));
        Assert.True(RestSiteDecisionPolicy.Contains([extension], new ForwardingDecision(decision)));
        Assert.True(RestSiteDecisionPolicy.Contains(builtIns, new RestSiteDecision.Smith(firstCard)));
        Assert.False(RestSiteDecisionPolicy.Contains(builtIns,
            new RestSiteDecision.Smith((CardModel)firstCard.MutableClone())));
        var remaining = builtIns.ToList();
        foreach (RestSiteDecision expected in new[] { builtIns[2], builtIns[3], builtIns[1], builtIns[0] })
        {
            Assert.Same(expected, RestSiteDecisionPolicy.ChooseDefault(remaining));
            remaining.Remove(expected);
        }

        await new RestSiteRoom().ResolveAsync(player, extension);

        Assert.Equal(hp, player.Creature.CurrentHp);
        Assert.Equal(maxHp, player.Creature.MaxHp);
        Assert.Equal(deckCount, player.Deck.Cards.Count);
        Assert.Equal(upgrades, firstCard.CurrentUpgradeLevel);
        Assert.Same(firstCard, player.Deck.Cards[0]);
        Assert.Equal(pets, player.Relics.OfType<Byrdpip>().Count());
        Assert.Equal(pets, player.Deck.Cards.OfType<ByrdSwoop>().Count());
        Assert.Empty(player.Deck.Cards.OfType<ByrdonisEgg>());
        if (optionId == "cook")
        {
            Assert.DoesNotContain(penultimateCard, player.Deck.Cards);
            Assert.DoesNotContain(lastCard, player.Deck.Cards);
        }
    }

    private sealed record ForwardingDecision(RestSiteDecision Inner) : RestSiteDecision
    {
        public override string OptionId => Inner.OptionId;
        public override int Priority => -1;
        public override string GetLabel(Player player) => "content:" + Inner.GetLabel(player);
        public override Task ExecuteAsync(Player player) => Inner.ExecuteAsync(player);
    }
}
