using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Transfigure</c>：1 费、消耗（升级移除消耗），从手牌选 1 张：非 X 费且基础费用非负时
/// 本场战斗费用 +1，然后额外重放次数 +1。不能在战斗中生成。</summary>
public sealed class Transfigure : CardModel, ICardChoiceBaseValueProvider
{
    private const int EnergyIncrease = 1;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool CanBeGeneratedInCombat => false;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: EnergyIncrease);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards, 1, 1, this);
        foreach (CardModel card in selected)
        {
            // 原版 EnergyCost.GetWithModifiers(None) 只读基础费用：负数（不可打出）原样返回，其余不小于 0。
            if (!card.CostsXEnergy && card.CanonicalEnergyCostValue >= 0)
            {
                card.AddEnergyCostThisCombat(EnergyIncrease);
            }

            card.BaseReplayCount++;
        }
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
