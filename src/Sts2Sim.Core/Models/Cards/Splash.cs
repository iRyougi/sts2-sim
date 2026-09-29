using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>生成3张他系攻击牌选1免费进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Splash</c>）。
///
/// 偏离 #98 已销案（偏离 #302，2026-09-08）：原实现从扁平的"全部非无色卡"里抽，理由是
/// "本项目只实现了储君一个角色"。静默猎手落地后该前提消失，现按权威源码走
/// <c>UnlockState.CharacterCardPools</c>——池数 &gt; 1 时移除本角色卡池，只从他系角色池抽。
///
/// 偏离 #97：省略"本次出牌免费"。</summary>
public sealed class Splash : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState!;
        bool isMultiplayer = combatState.RunState.Players.Count > 1;

        // 权威：池数 > 1 才移除本角色卡池；只剩一个角色时保留自己的池。
        List<CardPoolModel> pools = Owner.UnlockState.CharacterCardPools.ToList();
        if (pools.Count > 1)
        {
            pools.Remove(Owner.Character.CardPool);
        }

        IEnumerable<CardModel> candidates = pools
            .SelectMany(pool => pool.GetUnlockedCards(Owner.UnlockState, isMultiplayer))
            .Where(card => card.Type == CardType.Attack);

        IReadOnlyList<CardModel> generated = CardFactory.GetDistinctForCombat(
            Owner,
            candidates,
            3,
            combatState.RunState.Rng.CombatCardGeneration);
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
