using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

/// <summary>Ironclad4 potions in native v0.111.0 order.</summary>
public sealed class IroncladPotionPool : PotionPoolModel
{
    public const string Ironclad4EpochId = "IRONCLAD4_EPOCH";

    private static readonly Type[] PotionTypes =
        [typeof(BloodPotion), typeof(SoldiersStew), typeof(Ashwater)];

    public override IReadOnlyList<PotionModel> AllPotions => PotionTypes
        .Select(type => (PotionModel)ModelDb.Get(type)).ToArray();

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Ironclad4EpochId) ? AllPotions : [];
}
