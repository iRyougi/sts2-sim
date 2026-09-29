using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Eidolon</c>：2 费（升级 1）消耗技能，按消耗堆当前顺序逐张自动打出其中带虚无且不带不能打出的牌。
/// 候选在打出前一次性快照；原版 <c>CardCmd.AutoPlay</c> 在战斗结束中或持有者死亡时对后续每张都直接返回。
/// 战斗中不能生成。</summary>
public sealed class Eidolon : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool CanBeGeneratedInCombat => false;

    protected override int CanonicalEnergyCost => 2;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<CardModel> etherealCards = Owner.PlayerCombatState!.ExhaustPile.Cards
            .Where(card => card.Keywords.Contains(CardKeyword.Ethereal) && !card.Keywords.Contains(CardKeyword.Unplayable))
            .ToList();
        foreach (CardModel card in etherealCards)
        {
            if (CombatState!.IsOverOrEnding() || Owner.Creature.IsDead)
                continue;
            await AutoPlayCmd.FromCards(CombatState!, Owner, [card]);
        }
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
