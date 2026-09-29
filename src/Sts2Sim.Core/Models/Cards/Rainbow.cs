using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Rainbow : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OrbCmd.Channel<LightningOrb>(CombatState!, Owner);
        await OrbCmd.Channel<FrostOrb>(CombatState!, Owner);
        await OrbCmd.Channel<DarkOrb>(CombatState!, Owner);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
