using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Retain;若仅剩此1张手牌则抽2张+2能量。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Restlessness</c>）。</summary>
public sealed class Restlessness : CardModel
{
    private int _draw = 2;
    private decimal _energy = 2m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        // 打出这张卡时,PlayAsync 已经把 this 从手牌移到出牌堆,所以"这是手牌里唯一一张牌"
        // 等价于此刻手牌已经空了（不需要再排除 this 自己）。
        bool isOnlyCardInHand = Owner.PlayerCombatState!.Hand.Cards.Count == 0;
        if (!isOnlyCardInHand)
        {
            return;
        }

        for (int i = 0; i < _draw; i++)
        {
            await CardPileCmd.Draw(CombatState!, 1, Owner, fromHandDraw: false);
        }
        Owner.PlayerCombatState!.GainEnergy(_energy);
    }

    protected override void OnUpgrade()
    {
        _draw += 1;
        _energy += 1m;
    }
}
