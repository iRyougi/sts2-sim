using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FrozenEgg : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsAllowed(IRunState runState) => RelicModel.IsBeforeAct3TreasureChest(runState);

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option,
        CardCreationOptions creationOptions) =>
        creationOptions.Flags.HasFlag(CardCreationFlags.NoHookUpgrades)
            ? null
            : TryModifyCardRewardOptionLate(runState, player, option);

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        if (!ReferenceEquals(player, Owner) || option.Type != CardType.Power || !option.IsUpgradable)
        {
            return null;
        }

        var upgraded = (CardModel)option.MutableClone();
        CardCmd.Upgrade(upgraded);
        return upgraded;
    }

    public override void ModifyMerchantCardCreationResults(Player player, List<CardModel> cards)
    {
        if (!ReferenceEquals(player, Owner)) return;
        foreach (CardModel card in cards.Where(card => card.Type == CardType.Power))
        {
            CardCmd.Upgrade(card);
        }
    }

    public override bool TryModifyCardBeingAddedToDeck(CardModel card, out CardModel? newCard)
    {
        newCard = null;
        if (!ReferenceEquals(card.Owner, Owner) || card.Type != CardType.Power || !card.IsUpgradable)
        {
            return false;
        }

        newCard = (CardModel)card.MutableClone();
        CardCmd.Upgrade(newCard);
        return true;
    }
}
