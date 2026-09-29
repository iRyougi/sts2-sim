using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>生成3张无色卡选1张进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Quasar</c>），
/// 选择交给运行时决策源。</summary>
public sealed class Quasar : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override int CanonicalStarCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState!;
        List<CardModel> candidates = ColorlessCardPool.Instance.GetUnlockedCards(
                Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .ToList();

        IReadOnlyList<CardModel> generated = CardFactory.GetDistinctForCombat(
            Owner, candidates, 3, combatState.RunState.Rng.CombatCardGeneration);
        if (IsUpgraded)
        {
            foreach (CardModel card in generated)
            {
                card.Upgrade();
            }
        }

        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(combatState, Owner, generated, 0, 1, this, cancelable: true)).FirstOrDefault();
        if (selected is not null)
        {
            await CardPileCmd.Generate(combatState, selected, PileType.Hand);
        }
    }
}
