using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Aeonglass status with gameplay-complete repeatable FakeUpgrade damage scaling.
/// Presentation-only FakeUpgrade art/title switching is tracked by deviation #281.
/// </summary>
public sealed class Wither : CardModel
{
    private int _fakeUpgradeLevel;

    public int FakeUpgradeLevel => _fakeUpgradeLevel;
    public int TurnEndDamage => 3 + 3 * _fakeUpgradeLevel;

    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };
    protected override bool HasTurnEndInHandEffect => true;

    public void FakeUpgrade()
    {
        AssertMutable();
        _fakeUpgradeLevel++;
    }

    protected override async Task OnTurnEndInHand()
    {
        await CreatureCmd.Damage(
            CombatState ?? throw new InvalidOperationException("Wither turn-end damage requires active combat."),
            new[] { Owner.Creature }, TurnEndDamage,
            ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature, this, null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_fakeUpgradeLevel);
}
