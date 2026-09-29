using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Encounters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class PunchOff : EventModel
{
    private IReadOnlyList<Reward> _combatRewards = EmptyRewards();

    public override bool IsAllowed(IRunState runState) => runState.TotalFloor >= 6;

    public override EventLayoutType LayoutType => EventLayoutType.Combat;

    public override EncounterDefinition? CanonicalEncounter => PunchOffEventEncounter.Definition;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new("NAB", Nab),
        new("I_CAN_TAKE_THEM", TakeThem),
    ];

    public override bool GenerateForcedCombatRewards => true;

    public override IReadOnlyList<Reward> ForcedCombatExtraRewards => _combatRewards;

    private async Task Nab()
    {
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Injury>()], Owner);
        await RewardsCmd.OfferCustom(Owner, [new RelicReward(Owner)]);
        Finish();
    }

    private Task TakeThem()
    {
        SetOptions(
            [new("FIGHT", Fight)]);
        return Task.CompletedTask;
    }

    private Task Fight()
    {
        _combatRewards =
        [
            new RelicReward(Owner),
            new PotionReward(Owner),
        ];
        // 经由 EventRoom 进入时，战斗复用进房时预创建的敌人（原版 CombatStateForLayout），不会调用这个工厂；
        // 只有脱离房间直接驱动事件时才用它生成同一批怪物。
        EncounterDefinition encounter = PunchOffEventEncounter.Definition;
        ulong encounterSeed = RoomFactory.EncounterMonsterSeed(RunState.Rng.Seed, RunState.TotalFloor, encounter);
        RequestForcedCombatBatch(() => encounter
            .CreateMonsters(new Rng(encounterSeed))
            .Select(entry => entry.Monster)
            .ToArray());
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
