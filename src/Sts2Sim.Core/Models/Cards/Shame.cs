using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Shame : CardModel
{
    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };
    protected override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand()
    {
        bool alreadyHasFrail = Owner.Creature.HasPower<FrailPower>();
        PowerModel? applied = await PowerCmd.Apply<FrailPower>(
            CombatState ?? throw new InvalidOperationException(
                "Shame turn-end effect requires active combat."),
            Owner.Creature,
            1m,
            null,
            this);
        if (applied is not null && !alreadyHasFrail)
        {
            applied.SkipNextDurationTick = true;
        }
    }
}
