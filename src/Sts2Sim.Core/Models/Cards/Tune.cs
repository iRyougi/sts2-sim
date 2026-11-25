using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>施加 1（升级 2）层 TunePower：每回合首次打出 Ki 后随机升级手牌（v0.114.0 原版 <c>Tune</c>）。</summary>
public sealed class Tune : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private int Cards => IsUpgraded ? 2 : 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<TunePower>(CombatState!, Owner.Creature, Cards, Owner.Creature, this);
    }
}
