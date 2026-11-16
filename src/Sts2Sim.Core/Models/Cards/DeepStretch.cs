using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得 Stretch 并抽 1 张牌（原版 <c>DeepStretch</c>）。</summary>
public sealed class DeepStretch : CardModel, ICardChoiceBaseValueProvider
{
    private const int Cards = 1;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal StretchAmount => IsUpgraded ? 10m : 7m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<StretchPower>(CombatState!, Owner.Creature, StretchAmount, Owner.Creature, this);
        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
    }
}
