using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class TheFutureOfPotions : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => VisibleDecisionMetadata.Empty;

    protected override VisibleOption DescribeVisibleOption(EventOption option, int displayIndex, VisibleOption declared)
    {
        if (option.Key != "POTION") return base.DescribeVisibleOption(option, displayIndex, declared);
        // Insertion order is the original displayed potion order. Only the first three
        // mappings have displayed options; never export the hidden remaining mappings.
        var displayed = _cardTypes.Take(3).ElementAt(displayIndex);
        PotionModel potion = displayed.Key;
        CardRarity rarity = potion.Rarity switch
        {
            PotionRarity.Common or PotionRarity.Token => CardRarity.Common,
            PotionRarity.Uncommon => CardRarity.Uncommon,
            PotionRarity.Rare or PotionRarity.Event => CardRarity.Rare,
            _ => throw new InvalidOperationException("Unsupported potion rarity."),
        };
        return new(option.Key, [VisibleValue.Id("PotionId", potion.Id.Entry), VisibleValue.Scope("SourcePotionRarity", potion.Rarity.ToString()),
            VisibleValue.Scope("TargetCardRarity", rarity.ToString()), VisibleValue.Scope("TargetCardType", displayed.Value.ToString()),
            VisibleValue.Condition("Upgrade", "true"), VisibleValue.Int("SelectCount", 1)])
            { Contents = [VisibleContentProjection.ForPotion(potion)] };
    }

    private Dictionary<PotionModel, CardType> _cardTypes = [];
    protected override bool LocksPotions => true;
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(p => p.PotionSlots.OfType<PotionModel>().Count() >= 2);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var potions = Owner.PotionSlots.OfType<PotionModel>().ToList();
        foreach (var potion in potions)
        {
            CardType[] types = potion.Rarity is PotionRarity.Common or PotionRarity.Token
                ? ([CardType.Attack, CardType.Skill]) : [CardType.Attack, CardType.Skill, CardType.Power];
            _cardTypes.Add(potion, Rng.NextItem(types));
        }
        return potions.Take(3).Select(potion => new EventOption("POTION", () => Trade(potion))).ToArray();
    }

    private async Task Trade(PotionModel potion)
    {
        CardRarity rarity = potion.Rarity switch
        {
            PotionRarity.Common or PotionRarity.Token => CardRarity.Common,
            PotionRarity.Uncommon => CardRarity.Uncommon,
            PotionRarity.Rare or PotionRarity.Event => CardRarity.Rare,
            _ => throw new InvalidOperationException("Unsupported potion rarity."),
        };
        await PotionCmd.DiscardForEvent(potion);
        var reward = new CardReward(Owner,
            CardCreationOptions.ForNonCombatWithUniformOdds([Owner.Character.CardPool],
                c => c.Rarity == rarity && c.Type == _cardTypes[potion])
                .WithFlags(CardCreationFlags.NoRarityModification | CardCreationFlags.NoCardPoolModifications));
        reward.Populate(RunState);
        foreach (var card in reward.Options)
            if (card.IsUpgradable) card.Upgrade();
        OfferRewards(RewardsSet.CreateCustom(Owner, card: reward));
        Finish();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _cardTypes = [];
    }
}
