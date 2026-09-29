using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Void : CardModel
{
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable, CardKeyword.Ethereal };

    public override Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (ReferenceEquals(card, this))
        {
            Owner.PlayerCombatState!.LoseEnergy(1m);
        }

        return Task.CompletedTask;
    }
}
