using System.Collections.ObjectModel;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

/// <summary>Silent-specific potions, gated behind the Silent4 epoch exactly as upstream.</summary>
public sealed class SilentPotionPool : PotionPoolModel
{
    public const string Silent4EpochId = "SILENT4_EPOCH";

    private static readonly Type[] PotionTypes =
        [typeof(PoisonPotion), typeof(GhostInAJar), typeof(CunningPotion)];

    private static int _poolGeneration;
    private static ReadOnlyCollection<PotionModel>? _poolCache;

    public override IReadOnlyList<PotionModel> AllPotions =>
        ModelDb.ResolveRegisteredPool(PotionTypes, ref _poolGeneration, ref _poolCache);

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Silent4EpochId) ? AllPotions : [];
}
