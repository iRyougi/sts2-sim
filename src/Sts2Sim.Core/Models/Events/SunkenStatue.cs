using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 event offering the Sword of Stone or a dangerous cache of gold. 偏离 #148：不移植
/// DynamicVars 容器，事件随机金币使用私有字段，伤害使用玩法常量。</summary>
public sealed class SunkenStatue : EventModel
{
    private decimal _gold;

    protected override void CalculateVars() => _gold = 111m + Rng.NextInt(-10, 11);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("GRAB_SWORD", GrabSwordAsync),
        new EventOption("DIVE_INTO_WATER", DiveIntoWaterAsync),
    };

    private async Task GrabSwordAsync()
    {
        await RelicCmd.Obtain(ModelDb.Relic<SwordOfStone>(), Owner);
        Finish();
    }

    private async Task DiveIntoWaterAsync()
    {
        await PlayerCmd.GainGold(_gold, Owner);
        await CreatureCmd.Damage(
            RunState,
            Owner.Creature,
            7m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        Finish();
    }
}
