using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Coolheaded : CardModel, ICardChoiceBaseValueProvider
{
    private int _cards = 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: _cards);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OrbCmd.Channel<FrostOrb>(CombatState!, Owner);
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
    }
    protected override void OnUpgrade() => _cards += 1;
}
