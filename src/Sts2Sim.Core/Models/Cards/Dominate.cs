using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Dominate : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    private decimal _vulnerable = 1m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, _vulnerable, Owner.Creature, this);
        int totalVulnerable = cardPlay.Target.GetPower<VulnerablePower>()?.Amount ?? 0;
        await PowerCmd.Apply<StrengthPower>(CombatState!, Owner.Creature, totalVulnerable, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _vulnerable++;
}
