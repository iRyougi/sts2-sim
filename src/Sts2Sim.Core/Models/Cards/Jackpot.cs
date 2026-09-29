using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>25伤害，生成3张0费卡进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Jackpot</c>）——
/// 与 Quasar/Discovery/Splash 不同，这里是"可重复选中同一候选"（真实源码用 GetForCombat，不排除已选中项），
/// 3张都直接进手牌，不需要玩家二次选择。</summary>
public sealed class Jackpot : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 25m;
    private const int GeneratedCount = 3;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        ICombatState combatState = CombatState!;
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        // 偏离 #319（2026-09-09）：权威走 Owner.Character.CardPool，不是扁平的全职业池。
        List<CardModel> candidates = CardPoolFilters
            .ForCombatGeneration(Owner.Character.CardPool.GetUnlockedCards(
                Owner.UnlockState,
                isMultiplayer: combatState.RunState.Players.Count > 1))
            .Where(card => !card.IsColorless && card.EnergyCost == 0 && !card.CostsXEnergy)
            .ToList();
        if (candidates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < GeneratedCount; i++)
        {
            CardModel picked = combatState.RunState.Rng.CombatCardGeneration.NextItem(candidates)!;
            var clone = (CardModel)picked.MutableClone();
            clone.AssignOwner(Owner);
            if (IsUpgraded)
            {
                clone.Upgrade();
            }

            await CardPileCmd.Generate(combatState, clone, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => _damage += 5m;
}
