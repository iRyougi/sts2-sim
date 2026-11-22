using System.Collections.Immutable;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

/// <summary>
/// Hive off-color reward event.
/// Deviation #202: <c>PlayerUnlockState</c> has no unlocked-character/card-pool set, so every registered
/// playable character is treated as unlocked.
/// </summary>
public sealed class ColorfulPhilosophers : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.UnlockState.CharacterCardPools.Count(pool => pool is not FoolCardPool) > 1);

    protected override VisibleDecisionMetadata BuildVisibleState() => new(
        [VisibleValue.Int("StatueCount", 3)],
        CurrentOptions.Select(option => new VisibleOption(option.Key,
            [VisibleValue.Int("Cards", 3), VisibleValue.Id("Character", option.Key),
             VisibleValue.Scope("RewardRarities", "Common,Uncommon,Rare")])).ToImmutableArray());

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

    /// <summary>#1039: one custom rewards screen holding three card rewards (common, uncommon, rare), populated
    /// in that order by <see cref="RewardsCmd.OfferCustom"/> (game <c>ColorfulPhilosophers.OfferRewards</c>).</summary>
    private async Task OfferAsync(CardPoolModel pool)
    {
        var rewards = new List<Reward>(3);
        foreach (CardRarity rarity in new[] { CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare })
        {
            var options = new CardCreationOptions([pool], CardCreationSource.Other,
                CardRarityOddsType.Uniform, card => card.Rarity == rarity)
                .WithFlags(CardCreationFlags.NoRarityModification |
                    CardCreationFlags.NoCardPoolModifications);
            rewards.Add(new CardReward(Owner, options, 3));
        }
        await RewardsCmd.OfferCustom(Owner, rewards);
        Finish();
    }

    private static int ColorOrder(string id) => id switch
    {
        "NECROBINDER" => 0,
        "IRONCLAD" => 1,
        "REGENT" => 2,
        "FOOL" => 3,
        "SILENT" => 4,
        "DEFECT" => 5,
        _ => 6,
    };
}
