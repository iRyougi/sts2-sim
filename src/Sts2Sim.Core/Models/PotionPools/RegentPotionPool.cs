using System.Collections.ObjectModel;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

public sealed class RegentPotionPool : PotionPoolModel
{
    public const string Regent4EpochId = "REGENT4_EPOCH";
    private static readonly Type[] PotionTypes = [typeof(StarPotion), typeof(CosmicConcoction), typeof(KingsCourage)];

    private static int _poolGeneration;
    private static ReadOnlyCollection<PotionModel>? _poolCache;

    public override IReadOnlyList<PotionModel> AllPotions =>
        ModelDb.ResolveRegisteredPool(PotionTypes, ref _poolGeneration, ref _poolCache);

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Regent4EpochId) ? AllPotions : [];
}
