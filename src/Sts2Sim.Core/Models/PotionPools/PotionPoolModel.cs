using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.PotionPools;

public abstract class PotionPoolModel
{
    public abstract IReadOnlyList<PotionModel> AllPotions { get; }

    public virtual IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState) => AllPotions;
}

internal sealed class EmptyPotionPool : PotionPoolModel
{
    public static EmptyPotionPool Instance { get; } = new();
    public override IReadOnlyList<PotionModel> AllPotions => [];
}
