using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>CallOfTheVoid</c>：1 费能力，施加 1 层 <see cref="CallOfTheVoidPower"/>；升级获得固有。</summary>
public sealed class CallOfTheVoid : CardModel, ICardChoiceBaseValueProvider
{
    private const int Cards = 1;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<CallOfTheVoidPower>(CombatState!, Owner.Creature, Cards, Owner.Creature, this);

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
