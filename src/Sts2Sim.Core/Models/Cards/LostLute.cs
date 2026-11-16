using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>每回合首次打出各旋律时从抽牌堆顶自动打出牌（原版 <c>LostLute</c>）。</summary>
public sealed class LostLute : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<LostLutePower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
