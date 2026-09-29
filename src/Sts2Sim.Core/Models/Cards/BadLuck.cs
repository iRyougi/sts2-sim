using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BadLuck : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedByModifiers => false;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Eternal, CardKeyword.Unplayable };
    protected override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand()
    {
        await CreatureCmd.Damage(
            CombatState ?? throw new InvalidOperationException("BadLuck turn-end damage requires active combat."),
            new[] { Owner.Creature }, 13m,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature, this, null);
    }
}
