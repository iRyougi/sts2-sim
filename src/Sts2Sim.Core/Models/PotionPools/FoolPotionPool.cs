using System.Collections.ObjectModel;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

/// <summary>v0.113.1（d09d48e5）Fool4 药水池与 FOOL4_EPOCH 解锁门（原版 <c>FoolPotionPool</c>）。</summary>
public sealed class FoolPotionPool : PotionPoolModel
{
    public const string Fool4EpochId = "FOOL4_EPOCH";

    private static readonly Type[] PotionTypes =
        [typeof(PrecisionPotion), typeof(BottledSong), typeof(CrystallineEuphoria)];

    private static int _poolGeneration;
    private static ReadOnlyCollection<PotionModel>? _poolCache;

    public override IReadOnlyList<PotionModel> AllPotions =>
        ModelDb.ResolveStrictPool(PotionTypes, ref _poolGeneration, ref _poolCache);

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Fool4EpochId) ? AllPotions : [];
}
