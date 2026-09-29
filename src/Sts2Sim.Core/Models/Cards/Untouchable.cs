using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>The authoritative source is pure numeric block/static Sly metadata.</summary>
public sealed class Untouchable : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 6m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
    protected override Task OnPlay(CardPlay cardPlay) =>
        CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
    protected override void OnUpgrade() => _block += 3m;
}
