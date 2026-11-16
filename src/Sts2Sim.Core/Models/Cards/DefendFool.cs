using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Fool 基础防御（原版 <c>MegaCrit.Sts2.Core.Models.Cards.DefendFool</c>）。</summary>
public sealed class DefendFool : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 5m;

    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Basic;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Defend];
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);

    protected override Task OnPlay(CardPlay cardPlay) =>
        CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);

    protected override void OnUpgrade() => _block += 3m;
}
