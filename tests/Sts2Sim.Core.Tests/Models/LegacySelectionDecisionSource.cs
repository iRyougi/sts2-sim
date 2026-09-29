using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Models;

/// <summary>Explicit legacy-test policy; production combat remains fail-closed.</summary>
internal sealed class LegacySelectionDecisionSource(bool chooseZero = false) : ICardSelectionDecisionSource
{
    public List<CardSelectionRequest> Requests { get; } = new();
    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
    {
        Assert.NotNull(request.Source);
        Assert.All(request.Candidates, card => Assert.Same(request.Player, card.Owner));
        Requests.Add(request);
        IReadOnlyList<CardModel> selected = chooseZero ? Array.Empty<CardModel>() : request.Candidates.Take(request.MaxCount).ToArray();
        return Task.FromResult(selected);
    }
}
