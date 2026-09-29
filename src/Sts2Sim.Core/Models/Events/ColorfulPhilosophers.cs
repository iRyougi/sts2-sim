using Sts2Sim.Core.Events;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Events;

/// <summary>
/// Hive off-color reward event.
/// Deviation #202: <c>PlayerUnlockState</c> has no unlocked-character/card-pool set, so every registered
/// playable character is treated as unlocked. Missing CardCreationOptions flags are represented by the
/// fixed-rarity CardFactory overload, preserving three distinct choices and the Rewards RNG stream.
/// </summary>
public sealed class ColorfulPhilosophers : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        ModelDb.All<CharacterModel>().Count(character => character.IsPlayable) > 1;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var candidates = ModelDb.All<CharacterModel>()
            .Where(character => character.IsPlayable && character.GetType() != Owner.Character.GetType())
            .OrderBy(character => ColorOrder(character.Id.Entry))
            .ToList();
        while (candidates.Count > 3)
        {
            candidates.RemoveAt(Rng.NextInt(candidates.Count));
        }

        return candidates
            .Select(character => new EventOption(character.Id.Entry, () => OfferAsync(character.CardPool)))
            .ToArray();
    }

    private Task OfferAsync(CardPoolModel pool)
    {
        foreach (CardRarity rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
        {
            IReadOnlyList<CardModel> options = CardFactory.CreateForReward(Owner, 3, pool, rarity);
            var cardReward = new CardReward(Owner, options);
            cardReward.Populate(RunState);
            OfferRewards(RewardsSet.CreateCustom(Owner, card: cardReward));
        }
        Finish();
        return Task.CompletedTask;
    }

    private static int ColorOrder(string id) => id switch
    {
        "NECROBINDER" => 0,
        "IRONCLAD" => 1,
        "REGENT" => 2,
        "SILENT" => 3,
        "DEFECT" => 4,
        _ => 5,
    };
}
