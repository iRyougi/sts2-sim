using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Hive automaton event. Reward filtering follows the authoritative Power / non-X zero-cost predicates.</summary>
public sealed class InfestedAutomaton : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("STUDY", [VisibleValue.Scope("RewardKind", "Card"), VisibleValue.Int("Count", 1),
            VisibleValue.Scope("CardType", "Power"), VisibleValue.Condition("Targets", "Random")]),
        new("TOUCH_CORE", [VisibleValue.Scope("RewardKind", "Card"), VisibleValue.Int("Count", 1),
            VisibleValue.Int("EnergyCost", 0), VisibleValue.Condition("CostsX", "False"), VisibleValue.Condition("Targets", "Random")]),
    ]);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("STUDY", () => AddRandomAsync(card => card.Type == CardType.Power)),
        new EventOption("TOUCH_CORE", () => AddRandomAsync(card => card.EnergyCost == 0 && !card.CostsXEnergy)),
    };

    private async Task AddRandomAsync(Func<CardModel, bool> predicate)
    {
        CardModel? card = CardFactory.CreateForReward(
            Owner, 1, Owner.Character.CardPool, CardRarityOddsType.RegularEncounter,
            predicate, noUpgradeRoll: true).SingleOrDefault();
        if (card is not null)
        {
            await CardPileCmd.AddToDeck(card);
        }
        Finish();
    }
}
