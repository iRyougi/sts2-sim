using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class RanwidTheElder : EventModel
{
    protected override bool LocksPotions => true;
    public PotionModel? PotionOffered { get; private set; }
    public RelicModel? RelicOffered { get; private set; }
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: > 0 } &&
        runState.Players.All(p => p.Gold >= 100 && p.Relics.Any(r => r.IsTradable) && p.PotionSlots.Any(x => x is not null));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        PotionOffered = Rng.NextItem(Owner.PotionSlots.OfType<PotionModel>());
        RelicOffered = Rng.NextItem(Owner.Relics.Where(r => r.IsTradable));
        return [new(PotionOffered is null ? "POTION_LOCKED" : "POTION", PotionOffered is null ? null : GivePotion),
            new("GOLD", GiveGold),
            new(RelicOffered is null ? "RELIC_LOCKED" : "RELIC", RelicOffered is null ? null : GiveRelic)];
    }
    private async Task GivePotion()
    {
        await PotionCmd.DiscardForEvent(PotionOffered!);
        await Obtain(1);
    }
    private async Task GiveGold()
    {
        await PlayerCmd.LoseGold(100, Owner, GoldLossType.Spent);
        await Obtain(1);
    }
    private async Task GiveRelic()
    {
        Owner.RemoveRelicInternal(RelicOffered!);
        await RelicOffered!.AfterRemoved();
        await Obtain(2);
    }
    private async Task Obtain(int count)
    {
        for (int i = 0; i < count; i++) await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner), Owner);
        Finish();
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        PotionOffered = null;
        RelicOffered = null;
    }
}
