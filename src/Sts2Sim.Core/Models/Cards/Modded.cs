using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Modded : CardModel, ICardChoiceBaseValueProvider
{
    private int _cards = 1;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: _cards);

    protected override async Task OnPlay(CardPlay play)
    {
        await OrbCmd.AddSlots(Owner, 1);
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
        AddEnergyCostThisCombat(1);
    }

    protected override void OnUpgrade() => _cards += 1;
}
