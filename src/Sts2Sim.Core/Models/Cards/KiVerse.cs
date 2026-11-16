using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>使本回合接下来的旋律牌额外打出 1 次（原版 <c>KiVerse</c>）。</summary>
public sealed class KiVerse : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Ki];
    private decimal PowerAmount => IsUpgraded ? 2m : 1m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<KiVersePower>(CombatState!, Owner.Creature, PowerAmount, Owner.Creature, this);
    }
}
