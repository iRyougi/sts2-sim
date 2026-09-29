using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Timeline.Epochs;

namespace Sts2Sim.Core.Models.PotionPools;

public sealed class DefectPotionPool : PotionPoolModel
{
    public override IReadOnlyList<PotionModel> AllPotions => Defect4Epoch.Potions;

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Defect4Epoch.Id) ? AllPotions : [];
}
