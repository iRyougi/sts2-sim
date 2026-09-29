using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Regent starter skill. Grants two Stars.
/// </summary>
public sealed class Venerate : CardModel
{
    private decimal _starsGranted = 2m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override void OnUpgrade() => _starsGranted += 1m;

    protected override Task OnPlay(CardPlay cardPlay) => PlayerCmd.GainStars(_starsGranted, Owner);
}
