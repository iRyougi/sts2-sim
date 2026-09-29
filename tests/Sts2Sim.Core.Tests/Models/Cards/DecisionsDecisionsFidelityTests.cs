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
public sealed class DecisionsDecisionsFidelityTests : IDisposable
{
    public DecisionsDecisionsFidelityTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight), typeof(VelvetChoker), typeof(WanderingGrunt),
            typeof(DecisionsDecisions), typeof(LegSweep), typeof(WeakPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RejectedNestedPlays_DoNotConsumeCombatTargets()
    {
        var runState = new RunState("decisions-choker-target-order", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var choker = (VelvetChoker)ModelDb.Relic<VelvetChoker>().MutableClone();
        choker.AssignOwner(player);
        player.AddRelicInternal(choker);
        var room = new CombatRoom(() => Enumerable.Range(0, 2)
            .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone())
            .ToArray());
        await room.Enter(runState);

        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToArray())
            {
                CardPileCmd.Remove(card);
            }
        }

        for (int i = 0; i < 5; i++)
        {
            await AddCard<DefendRegent>(player, PileType.Hand).PlayAsync(target: null);
        }

        for (int i = 0; i < 3; i++)
        {
            AddCard<LegSweep>(player, PileType.Draw);
        }

        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)room.Engine.State).CardSelectionSource = selection;
        DecisionsDecisions decisions = AddCard<DecisionsDecisions>(player, PileType.Hand);
        int cardsBefore = player.PlayerCombatState.CardsPlayedThisTurn;
        int targetsBefore = runState.Rng.CombatTargets.Counter;

        await decisions.PlayAsync(target: null);

        Assert.Equal(5, cardsBefore);
        var request = Assert.Single(selection.Requests);
        LegSweep selected = Assert.IsType<LegSweep>(request.Candidates[0]);
        Assert.Equal(cardsBefore + 2, player.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Equal(PileType.Discard, selected.Pile!.Type);
        Assert.Equal(1, room.Engine.State.HittableEnemies.Count(enemy => enemy.Powers.OfType<WeakPower>().Any()));
        Assert.Equal(targetsBefore + 1, runState.Rng.CombatTargets.Counter);
    }

    private static TCard AddCard<TCard>(Player player, PileType pile)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }
}
