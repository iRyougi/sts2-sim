using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class AbyssalBaths : EventModel
{
    private const decimal BaseDamage = 3m;
    private const decimal DamageScaling = 1m;
    private const decimal MaxHpGain = 2m;
    private const decimal AbstainHeal = 10m;
    private int _lingerCount;
    private decimal _damage = BaseDamage;

    protected override void CalculateVars() => _damage = BaseDamage;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new("IMMERSE", Immerse),
        new("ABSTAIN", Abstain),
    ];

    private async Task Immerse()
    {
        await OnImmerse();
        SetOptions(
        [
            new("LINGER", Linger),
            new("EXIT_BATHS", ExitBaths),
        ]);
    }

    private async Task Abstain()
    {
        await CreatureCmd.Heal(Owner.Creature, AbstainHeal);
        Finish();
    }

    private async Task Linger()
    {
        _lingerCount = Math.Min(_lingerCount + 1, 9);
        await OnImmerse();
        SetOptions(
        [
            new("LINGER", Linger),
            new("EXIT_BATHS", ExitBaths),
        ]);
    }

    private Task ExitBaths()
    {
        Finish();
        return Task.CompletedTask;
    }

    private async Task OnImmerse()
    {
        await CreatureCmd.GainMaxHp(Owner.Creature, MaxHpGain);
        await CreatureCmd.Damage(
            RunState,
            Owner.Creature,
            _damage,
            ValueProp.Unblockable | ValueProp.Unpowered);
        _damage += DamageScaling;
    }
}
