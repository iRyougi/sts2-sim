using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Backflip: block, then draw two.</summary>
public sealed class Backflip : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 5m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await CardPileCmd.Draw(CombatState!, 2, Owner, fromHandDraw: false);
    }
    protected override void OnUpgrade() => _block += 3m;
}
