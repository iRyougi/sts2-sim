using System.Collections.ObjectModel;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

/// <summary>Necrobinder4 potions in native v0.111.0 order.</summary>
public sealed class NecrobinderPotionPool : PotionPoolModel
{
    public const string Necrobinder4EpochId = "NECROBINDER4_EPOCH";

    private static readonly Type[] PotionTypes =
        [typeof(PotionOfDoom), typeof(PotOfGhouls), typeof(BoneBrew)];

    private static int _poolGeneration;
    private static ReadOnlyCollection<PotionModel>? _poolCache;

    public override IReadOnlyList<PotionModel> AllPotions =>
        ModelDb.ResolveStrictPool(PotionTypes, ref _poolGeneration, ref _poolCache);

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Necrobinder4EpochId) ? AllPotions : [];
}
