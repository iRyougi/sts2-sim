using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Voltaic : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CombatState state = (CombatState)CombatState!;
        int count = state.SemanticHistory.CountOrbsChanneledThisCombat<LightningOrb>(state, Owner);
        for (int i = 0; i < count; i++)
            await OrbCmd.Channel<LightningOrb>(state, Owner);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
