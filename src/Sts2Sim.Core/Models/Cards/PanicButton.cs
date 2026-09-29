using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,30格挡,2回合内禁止再获格挡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.PanicButton</c>）。</summary>
public sealed class PanicButton : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 30m;
    private const decimal LockTurns = 2m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<NoBlockPower>(CombatState!, Owner.Creature, LockTurns, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _block += 10m;
}
