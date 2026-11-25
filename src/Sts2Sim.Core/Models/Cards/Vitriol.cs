using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>对目标施加 2（升级 3）层虚弱并向手牌生成 2 张 Sadness（v0.114.0 原版 <c>Vitriol</c>）。</summary>
public sealed class Vitriol : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    private decimal WeakAmount => IsUpgraded ? 3m : 2m;
    private const int Cards = 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, WeakAmount, Owner.Creature, this);
        // 原版 AddGeneratedCardsToCombat 逐张加入并逐张触发生成钩子，与逐张 Generate 等价。
        for (int i = 0; i < Cards; i++)
        {
            var sadness = (CardModel)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, sadness, PileType.Hand, Owner);
        }
    }
}
