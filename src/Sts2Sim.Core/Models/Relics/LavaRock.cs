using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LavaRock : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public bool HasTriggered { get; private set; }

    public override bool IsUsedUp => HasTriggered;

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (HasTriggered ||
            !ReferenceEquals(player, Owner) ||
            roomType != RoomType.Boss ||
            Owner.RunState is not RunState { CurrentActIndex: 0 } runState ||
            runState.CurrentRoom is not CombatRoom { RoomType: RoomType.Boss })
        {
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            var reward = new RelicReward(Owner);
            reward.Populate(runState);
            rewards.Add(reward);
        }

        HasTriggered = true;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(HasTriggered);
    }
}