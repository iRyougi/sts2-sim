using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Dirge</c>：X 费消耗技能。召唤 3（升级 4）共 X 次，再创建 X 张 <see cref="Soul"/>（升级时先升级它们），
/// 逐张随机洗入抽牌堆。</summary>
public sealed class Dirge : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override bool IsXEnergyCost => true;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private decimal Summon => IsUpgraded ? 4m : 3m;

    // Summon 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int xValue = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        for (int index = 0; index < xValue; index++)
            await OstyCmd.Summon(Owner, Summon, this);

        List<Soul> souls = Soul.Create(Owner, xValue);

        // 原版 CardCmd.Upgrade 在战斗结束中不升级；共享的 CardCmd.Upgrade 没有这道守卫。
        if (IsUpgraded && !CombatState!.IsOverOrEnding())
        {
            foreach (Soul soul in souls)
                CardCmd.Upgrade(soul);
        }

        foreach (Soul soul in souls)
            await CardPileCmd.Generate(CombatState!, soul, PileType.Draw, Owner, CardPilePosition.Random);
    }
}
