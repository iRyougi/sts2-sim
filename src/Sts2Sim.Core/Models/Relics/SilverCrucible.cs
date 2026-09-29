using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SilverCrucible : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public int TimesUsed { get; private set; }

    public int TreasureRoomsEntered { get; private set; }

    public override bool IsUsedUp => TimesUsed >= 3 && TreasureRoomsEntered > 0;

    public override bool IsAllowed(IRunState runState) => runState.Players.Count == 1;

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState, Player player, CardModel option, CardCreationOptions creationOptions) =>
        creationOptions.Flags.HasFlag(CardCreationFlags.IsCardReward)
            ? TryModifyCardRewardOptionLate(runState, player, option) : null;

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        if (!ReferenceEquals(player, Owner) || TimesUsed >= 3)
        {
            return null;
        }

        if (!option.IsUpgradable)
        {
            return option;
        }

        var clone = (CardModel)option.MutableClone();
        CardCmd.Upgrade(clone);
        return clone;
    }

    public override void AfterModifyingCardRewardOptions(
        IRunState runState,
        Player player,
        IReadOnlyList<CardModel> options)
    {
        if (ReferenceEquals(player, Owner) && TimesUsed < 3)
        {
            TimesUsed++;
        }
    }

    public override bool ShouldGenerateTreasure(Player player) =>
        !ReferenceEquals(player, Owner) || TreasureRoomsEntered > 1;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is TreasureRoom)
        {
            TreasureRoomsEntered++;
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(TimesUsed);
        builder.Append(TreasureRoomsEntered);
    }
}
