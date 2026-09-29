using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class OldCoin : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsAllowed(Runs.IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override bool HasUponPickupEffect => true;

    public override bool IsAllowedInShops => false;

    public override Task AfterObtained() => PlayerCmd.GainGold(300m, Owner);
}
