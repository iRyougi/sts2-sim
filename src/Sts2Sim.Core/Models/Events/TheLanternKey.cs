using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Encounters;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Models.Events;

public sealed class TheLanternKey : EventModel
{
    private IReadOnlyList<Reward> _combatRewards = EmptyRewards();

    public override EventLayoutType LayoutType => EventLayoutType.Combat;
    public override EncounterDefinition? CanonicalEncounter => MysteriousKnightEventEncounter.Definition;
    public override bool GenerateForcedCombatRewards => true;
    public override IReadOnlyList<Reward> ForcedCombatExtraRewards => _combatRewards;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("RETURN_THE_KEY", ReturnAsync),
        new EventOption("KEEP_THE_KEY", KeepAsync),
    ];

    private async Task ReturnAsync()
    {
        await PlayerCmd.GainGold(100m, Owner);
        Finish();
    }

    private Task KeepAsync()
    {
        SetOptions([new EventOption("FIGHT", FightAsync)]);
        return Task.CompletedTask;
    }

    private Task FightAsync()
    {
        var key = (LanternKey)ModelDb.Card<LanternKey>().MutableClone();
        key.AssignOwner(Owner);
        _combatRewards = [new SpecialCardReward(key, Owner)];
        RequestForcedCombat(MysteriousKnightEventEncounter.Definition.CreateMonster);
        SuspendForForcedCombat();
        return Task.CompletedTask;
    }

    protected override void AfterForcedCombat(ForcedCombatOutcome outcome)
    {
        _combatRewards = EmptyRewards();
        Finish();
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _combatRewards = EmptyRewards();
    }

    private static IReadOnlyList<Reward> EmptyRewards() =>
        new List<Reward>().AsReadOnly();
}
