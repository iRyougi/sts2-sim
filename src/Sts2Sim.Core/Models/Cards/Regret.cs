using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Regret : CardModel
{
    private int _cardsInHand;

    private int CardsInHand
    {
        get => _cardsInHand;
        set
        {
            AssertMutable();
            _cardsInHand = value;
        }
    }

    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };
    protected override bool HasTurnEndInHandEffect => true;

    public override async Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        await base.BeforeSideTurnEnd(side, participants);
        if (IsEligibleForTurnEndInHandEffect(side, participants))
        {
            CardsInHand = Owner.PlayerCombatState!.Hand.Cards.Count;
        }
    }

    protected override async Task OnTurnEndInHand()
    {
        await CreatureCmd.Damage(
            CombatState ?? throw new InvalidOperationException("Regret turn-end damage requires active combat."),
            new[] { Owner.Creature }, CardsInHand,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature, this, null);
        CardsInHand = 0;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _cardsInHand = 0;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(_cardsInHand == 0, nameof(_cardsInHand));
    }
}
