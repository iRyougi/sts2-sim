using Sts2Sim.Core.Events;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class RoundTeaParty : EventModel
{
    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Creature.CurrentHp >= 12);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("ENJOY_TEA", EnjoyTeaAsync),
        new EventOption("PICK_FIGHT", PickFightAsync),
    ];

    private async Task EnjoyTeaAsync()
    {
        await RelicCmd.Obtain(ModelDb.Relic<RoyalPoison>(), Owner);
        await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
        Finish();
    }

    private Task PickFightAsync()
    {
        SetOptions([new EventOption("CONTINUE_FIGHT", ContinueFightAsync)]);
        return Task.CompletedTask;
    }

    private async Task ContinueFightAsync()
    {
        await CreatureCmd.Damage(
            RunState,
            Owner.Creature,
            11m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        await RelicCmd.Obtain(RelicFactory.PullNextRelicFromFront(Owner), Owner);
        Finish();
    }
}
