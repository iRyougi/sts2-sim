using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Timeline.Epochs;

/// <summary>Defect's fourth epoch potion unlock data; the headless simulator has no EpochModel.</summary>
public static class Defect4Epoch
{
    public const string Id = "DEFECT4_EPOCH";

    public static IReadOnlyList<PotionModel> Potions =>
    [
        ModelDb.Potion<FocusPotion>(),
        ModelDb.Potion<EssenceOfDarkness>(),
        ModelDb.Potion<PotionOfCapacity>(),
    ];
}
