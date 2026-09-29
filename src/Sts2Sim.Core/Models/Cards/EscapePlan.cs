using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Escape Plan: block only when the actual drawn card is a Skill.</summary>
public sealed class EscapePlan : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 3m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? drawn = (await CardPileCmd.Draw(CombatState!, 1, Owner, fromHandDraw: false)).FirstOrDefault();
        if (drawn?.Type == CardType.Skill)
        {
            await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        }
    }
    protected override void OnUpgrade() => _block += 2m;
}
