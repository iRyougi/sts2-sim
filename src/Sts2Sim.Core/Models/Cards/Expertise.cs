using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Draw cards, then retain the whole hand through this turn's end.</summary>
public sealed class Expertise : CardModel
{
    private int _cards = 2;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
        await PowerCmd.Apply<RetainHandPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
    protected override void OnUpgrade() => _cards++;
}
