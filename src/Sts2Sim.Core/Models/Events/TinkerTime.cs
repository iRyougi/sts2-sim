using System.Collections.Immutable;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

public sealed class TinkerTime : EventModel
{
    private CardType _chosenType;

    protected override VisibleDecisionMetadata BuildVisibleState() => new([],
        CurrentOptions.Select(DescribeOffer).ToImmutableArray());

    private VisibleOption DescribeOffer(EventOption option)
    {
        if (option.Key == "CHOOSE_CARD_TYPE")
            return new(option.Key, [VisibleValue.Int("CardCount", 1), VisibleValue.Id("Card", ModelDb.GetId<MadScience>().Entry)]);

        CardType type;
        MadScienceRider rider;
        bool isTypeOffer = Enum.TryParse(option.Key, ignoreCase: true, out type);
        if (isTypeOffer) rider = MadScienceRider.None;
        else
        {
            type = _chosenType;
            rider = Enum.Parse<MadScienceRider>(option.Key, ignoreCase: true);
        }
        // A local description-only clone: Configure changes no RNG or source-world state.
        var preview = (MadScience)ModelDb.Card<MadScience>().MutableClone();
        preview.Configure(type, rider);
        ImmutableArray<VisibleValue> values = rider switch
        {
            MadScienceRider.Sapping => [VisibleValue.Int("SappingWeak", 2), VisibleValue.Int("SappingVulnerable", 2), VisibleValue.Id("WeakPower", ModelDb.GetId<Sts2Sim.Core.Models.Powers.WeakPower>().Entry), VisibleValue.Id("VulnerablePower", ModelDb.GetId<Sts2Sim.Core.Models.Powers.VulnerablePower>().Entry)],
            MadScienceRider.Violence => [VisibleValue.Int("AdditionalHits", 2)],
            MadScienceRider.Choking => [VisibleValue.Int("ChokingDamage", 6), VisibleValue.Scope("DamageTiming", "EachCardPlayedThisTurn"), VisibleValue.Id("Power", ModelDb.GetId<Sts2Sim.Core.Models.Powers.StranglePower>().Entry)],
            MadScienceRider.Energized => [VisibleValue.Int("EnergizedEnergy", 2)],
            MadScienceRider.Wisdom => [VisibleValue.Int("WisdomCards", 3)],
            MadScienceRider.Chaos => [VisibleValue.Int("GeneratedCardCount", 1), VisibleValue.Int("ThisTurnCost", 0), VisibleValue.Condition("GeneratedCard", "Random")],
            MadScienceRider.Expertise => [VisibleValue.Int("ExpertiseStrength", 2), VisibleValue.Int("ExpertiseDexterity", 2), VisibleValue.Id("StrengthPower", ModelDb.GetId<Sts2Sim.Core.Models.Powers.StrengthPower>().Entry), VisibleValue.Id("DexterityPower", ModelDb.GetId<Sts2Sim.Core.Models.Powers.DexterityPower>().Entry)],
            MadScienceRider.Curious => [VisibleValue.Int("CuriousReduction", 1)],
            MadScienceRider.Improvement => [VisibleValue.Int("UpgradeCount", 1), VisibleValue.Condition("UpgradeTarget", "Random"), VisibleValue.Scope("UpgradeTiming", "AfterCombat")],
            _ => [],
        };
        return new(option.Key, values.Add(VisibleValue.Int("CardCount", 1)))
        { Contents = [VisibleContentProjection.ForCard(preview)] };
    }


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
