using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Every second sacrificed card reward grants a relic from the reward grab bag.</summary>
public sealed class PaelsWing : RelicModel
{
    private int _rewardsSacrificed;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public int RewardsSacrificed => _rewardsSacrificed;
    public int DisplayAmount => _rewardsSacrificed % 2;

    public override bool TryModifyCardRewardAlternatives(
        Player player, CardReward reward, List<CardRewardAlternative> alternatives)
    {
        if (!ReferenceEquals(player, Owner)) return false;
        alternatives.Add(new CardRewardAlternative("SACRIFICE", OnSacrifice,
            () => ReferenceEquals(reward.Player, Owner) && Owner.Relics.Contains(this)));
        return true;
    }

    private async Task OnSacrifice()
    {
        AssertMutable();
        _rewardsSacrificed++;
        Flash();
        if (_rewardsSacrificed % 2 == 0)
            await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner), Owner);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_rewardsSacrificed);
}
