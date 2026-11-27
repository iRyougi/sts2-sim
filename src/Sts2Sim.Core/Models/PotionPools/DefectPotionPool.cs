using System.Collections.ObjectModel;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

public sealed class DefectPotionPool : PotionPoolModel
{
    public const string Defect4EpochId = "DEFECT4_EPOCH";

    private static readonly Type[] PotionTypes =
        [typeof(FocusPotion), typeof(EssenceOfDarkness), typeof(PotionOfCapacity)];

    private static int _poolGeneration;
    private static ReadOnlyCollection<PotionModel>? _poolCache;

    public override IReadOnlyList<PotionModel> AllPotions =>
        ModelDb.ResolveRegisteredPool(PotionTypes, ref _poolGeneration, ref _poolCache);

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Defect4EpochId) ? AllPotions : [];
}
