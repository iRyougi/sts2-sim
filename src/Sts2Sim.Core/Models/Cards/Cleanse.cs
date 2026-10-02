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
        CardModel? selected = (await CardSelectCmd.FromCombatPile(
            CombatState!, Owner, Owner.PlayerCombatState!.DrawPile, 1, 1, this)).FirstOrDefault();
        if (selected is not null)
            await CardPileCmd.Exhaust(CombatState!, selected);
    }
}
