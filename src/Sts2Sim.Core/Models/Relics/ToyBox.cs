using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Rewards;
namespace Sts2Sim.Core.Models.Relics;

public sealed class ToyBox : RelicModel
{
    private const int RelicCount = 5;
    private const int CombatsPerMelt = 3;

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;
    public override bool IsUsedUp => CombatsSeen >= RelicCount * CombatsPerMelt;
    public int CombatsSeen { get; private set; }
    public int DisplayAmount => CombatsSeen % CombatsPerMelt;

    public override async Task AfterObtained()
    {
        var rewards = new List<Reward>(RelicCount);
        for (int i = 0; i < RelicCount; i++)
        {
            RelicModel relic = (RelicModel)RelicFactory.PullNextRelicFromFront(Owner).MutableClone();
            relic.IsWax = true;
            rewards.Add(new RelicReward(relic, Owner));
        }
        await RewardsCmd.OfferCustom(Owner, rewards);
    }

    public override async Task AfterCombatEnd()
    {
        if (IsUsedUp)
            return;

        CombatsSeen++;
        if (CombatsSeen % CombatsPerMelt != 0)
            return;

        RelicModel? relic = Owner.Relics.FirstOrDefault(candidate =>
            candidate.IsWax && !candidate.IsMelted);
        if (relic is not null)
            await RelicCmd.Melt(relic);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(CombatsSeen);
}
