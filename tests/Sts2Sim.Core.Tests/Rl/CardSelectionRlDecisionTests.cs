using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rl;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rl;

[Collection("ModelDb")]
public sealed class CardSelectionRlDecisionTests : IDisposable
{
    public CardSelectionRlDecisionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void ObservationAndDecoder_ExposeEveryCandidateAndChooseNonFirst()
    {
        (RunState runState, Player player, CombatState state) = CreateState("task6-rl-core");
        StrikeRegent first = AddToDraw<StrikeRegent>(player);
        DefendRegent second = AddToDraw<DefendRegent>(player);
        var request = new CardSelectionRequest(
            player, new CardModel[] { first, second }, 1, 1, Source: null);

        ObservationSnapshot snapshot = ObservationEncoder.EncodeCardSelectionDecision(
            runState, request, request.Candidates, selectedCount: 0);

        Assert.Equal(DecisionType.CardSelection, snapshot.DecisionType);
        Assert.Equal(new[]
        {
            ActionSpaceLayout.CardSelectionIndex(0),
            ActionSpaceLayout.CardSelectionIndex(1),
        }, snapshot.Candidates.Select(candidate => candidate.SlotIndex));
        Assert.Equal(new[] { first.Id.ToString(), second.Id.ToString() },
            snapshot.Candidates.Select(candidate => candidate.Label));
        Assert.Same(second, ActionDecoder.DecodeCardSelectionChoice(
            ActionSpaceLayout.CardSelectionIndex(1), request.Candidates));
        Assert.Equal(ActionSpaceLayout.TotalActions, snapshot.LegalActionMask.Count);
        Assert.Same(state, player.Creature.CombatState);
    }

    [Fact]
    public void CardSelectionRange_IsAppendedWithoutMovingExistingProtocolSlots()
    {
        Assert.Equal(590, ActionSpaceLayout.EventBase);
        Assert.Equal(593, ActionSpaceLayout.CardSelectionBase);
        Assert.Equal(256, ActionSpaceLayout.MaxCardSelectionChoices);
        Assert.Equal(849, ActionSpaceLayout.PlayerTargetBase);
        Assert.Equal(913, ActionSpaceLayout.CardSelectionConfirmIndex);
        Assert.Equal(914, ActionSpaceLayout.CardSelectionCancelIndex);
        Assert.Equal(1077, ActionSpaceLayout.TotalActions);
        Assert.True(ActionSpaceLayout.TryDecodeCardSelection(
            ActionSpaceLayout.CardSelectionIndex(255), out int position));
        Assert.Equal(255, position);
    }

    [Fact]
    public void Observation_RejectsMoreThanStaticCapacityWithoutTruncatingOrReordering()
    {
        (RunState runState, Player player, _) = CreateState("task6-rl-capacity");
        CardModel[] candidates = Enumerable.Range(0, 257)
            .Select(_ =>
            {
                var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
                card.AssignOwner(player);
                return (CardModel)card;
            })
            .ToArray();
        var request = new CardSelectionRequest(player, candidates, 1, 1, Source: null);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            ObservationEncoder.EncodeCardSelectionDecision(
                runState, request, candidates, selectedCount: 0));

        Assert.Contains("257", error.Message);
        Assert.Contains("256", error.Message);
    }

    [Fact]
    public async Task AutonomousRunEnginePolicy_IsExplicitAndCanChooseNonFirstCandidates()
    {
        (_, Player player, _) = CreateState("task6-run-engine-policy");
        StrikeRegent first = AddToDraw<StrikeRegent>(player);
        DefendRegent second = AddToDraw<DefendRegent>(player);
        Alignment third = AddToDraw<Alignment>(player);
        var request = new CardSelectionRequest(
            player, new CardModel[] { first, second, third }, 2, 2, Source: null);

        IReadOnlyList<CardModel> selected = await
            RunEngineCardSelectionDecisionSource.Instance.ChooseCardsAsync(request);

        Assert.Equal(new CardModel[] { second, third }, selected);
    }

    private static (RunState RunState, Player Player, CombatState State) CreateState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        var state = new CombatState(runState);
        state.AddPlayerCreature(player.Creature);
        return (runState, player, state);
    }

    private static T AddToDraw<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Draw);
        return card;
    }
}
