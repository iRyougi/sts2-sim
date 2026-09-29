using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MawBank : RelicModel
{
    private bool _hasItemBeenBought;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool IsUsedUp => HasItemBeenBought;

    // SavedProperty/presentation status follow the existing simulator-wide omissions.
    public bool HasItemBeenBought
    {
        get => _hasItemBeenBought;
        set
        {
            AssertMutable();
            _hasItemBeenBought = value;
        }
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (Owner.RunState.BaseRoom == room && !HasItemBeenBought)
            await PlayerCmd.GainGold(12m, Owner);
    }

    public override Task AfterItemPurchased(Player player, MerchantEntry itemPurchased, int goldSpent)
    {
        if (player == Owner && !HasItemBeenBought && goldSpent > 0)
            HasItemBeenBought = true;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_hasItemBeenBought);
}
