using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

/// <summary>Silent-specific potions, gated behind the Silent4 epoch exactly as upstream.</summary>
public sealed class SilentPotionPool : PotionPoolModel
{
    public const string Silent4EpochId = "SILENT4_EPOCH";

    private static readonly Type[] PotionTypes =
        [typeof(PoisonPotion), typeof(GhostInAJar), typeof(CunningPotion)];

    public override IReadOnlyList<PotionModel> AllPotions => PotionTypes.Where(ModelDb.Contains)
        .Select(type => (PotionModel)ModelDb.Get(type)).ToArray();

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) =>
        unlockState.IsEpochRevealed(Silent4EpochId) ? AllPotions : [];
}
