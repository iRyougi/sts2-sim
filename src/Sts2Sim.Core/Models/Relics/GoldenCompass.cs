using Sts2Sim.Core.Entities.Relics;
namespace Sts2Sim.Core.Models.Relics;
/// <summary>Deviation #224: current-act map regeneration and GoldenPath forcing are not public run APIs.</summary>
public sealed class GoldenCompass : RelicModel { public override RelicRarity Rarity => RelicRarity.Ancient; }
