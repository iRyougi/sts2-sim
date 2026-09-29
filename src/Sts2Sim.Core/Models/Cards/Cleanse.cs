using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Cleanse</c>：1 费技能，召唤 3（升级 5），再从抽牌堆选 1 张消耗。</summary>
public sealed class Cleanse : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal Summon => IsUpgraded ? 5m : 3m;

    // Summon 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OstyCmd.Summon(Owner, Summon, this);
        // 原版 CardSelectCmd.FromCombatPile 在战斗结束中直接返回空选择。
        if (CombatState!.IsOverOrEnding())
            return;

        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            CombatState!, Owner, Owner.PlayerCombatState!.DrawPile.Cards, 1, 1, this)).FirstOrDefault();
        if (selected is not null)
            await CardPileCmd.Exhaust(CombatState!, selected);
    }
}
