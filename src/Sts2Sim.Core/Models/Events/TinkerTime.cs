using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

public sealed class TinkerTime : EventModel
{
    private CardType _chosenType;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new EventOption("CHOOSE_CARD_TYPE", ChooseCardTypeAsync)];

    private Task ChooseCardTypeAsync()
    {
        SetOptions(TakeTwo(new[] { CardType.Attack, CardType.Skill, CardType.Power })
            .Select(type => new EventOption(type.ToString().ToUpperInvariant(), () => ChooseTypeAsync(type)))
            .ToArray());
        return Task.CompletedTask;
    }

    private Task ChooseTypeAsync(CardType type)
    {
        _chosenType = type;
        MadScienceRider[] candidates = type switch
        {
            CardType.Attack => [MadScienceRider.Sapping, MadScienceRider.Violence, MadScienceRider.Choking],
            CardType.Skill => [MadScienceRider.Energized, MadScienceRider.Wisdom, MadScienceRider.Chaos],
            CardType.Power => [MadScienceRider.Expertise, MadScienceRider.Curious, MadScienceRider.Improvement],
            _ => throw new InvalidOperationException($"Unsupported Mad Science type {type}."),
        };
        SetOptions(TakeTwo(candidates)
            .Select(rider => new EventOption(rider.ToString().ToUpperInvariant(), () => CreateCardAsync(rider)))
            .ToArray());
        return Task.CompletedTask;
    }

    private async Task CreateCardAsync(MadScienceRider rider)
    {
        var card = (MadScience)ModelDb.Card<MadScience>().MutableClone();
        card.AssignOwner(Owner);
        card.Configure(_chosenType, rider);
        await CardPileCmd.AddToDeck(card);
        Finish();
    }

    private IReadOnlyList<T> TakeTwo<T>(IEnumerable<T> source)
    {
        List<T> candidates = source.ToList();
        Rng.Shuffle(candidates);
        return candidates.Take(2).ToArray();
    }
}
