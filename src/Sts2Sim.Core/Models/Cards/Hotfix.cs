using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Hotfix : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _focus = 2m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    protected override Task OnPlay(CardPlay play) =>
        PowerCmd.Apply<HotfixPower>(CombatState!, Owner.Creature, _focus, Owner.Creature, this);
    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
