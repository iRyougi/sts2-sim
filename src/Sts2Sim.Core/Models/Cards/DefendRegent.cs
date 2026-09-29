using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>基础格挡卡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.DefendRegent</c>）。</summary>
public sealed class DefendRegent : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 5m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => new[] { CardTag.Defend };

    protected override void OnUpgrade() => _block += 3m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
    }
}
