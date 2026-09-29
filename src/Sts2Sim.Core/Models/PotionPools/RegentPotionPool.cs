using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

public sealed class RegentPotionPool : PotionPoolModel
{
    public const string Regent4EpochId = "REGENT4_EPOCH";
    private static readonly Type[] PotionTypes = [typeof(StarPotion), typeof(CosmicConcoction), typeof(KingsCourage)];

    public override IReadOnlyList<PotionModel> AllPotions => PotionTypes.Where(ModelDb.Contains)
        .Select(type => (PotionModel)ModelDb.Get(type)).ToArray();

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Regent4EpochId) ? AllPotions : [];
}
