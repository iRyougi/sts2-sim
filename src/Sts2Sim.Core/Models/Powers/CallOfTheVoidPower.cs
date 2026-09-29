using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>CallOfTheVoidPower</c>：持有者抽手牌前，从本职业已解锁卡池（去掉基础与先古）里生成层数张牌，
/// 每张都加上虚无后放入手牌。原版每张单独调用一次 <c>GetDistinctForCombat(..., 1, CombatCardGeneration)</c>，
/// 所以每张都会重新洗一次整张候选表，且多张之间可能重复；全部生成完才依次加入手牌。</summary>
public sealed class CallOfTheVoidPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner.Player)
            return;

        List<CardModel> candidates = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1)
            .Where(card => card.Rarity is not (CardRarity.Basic or CardRarity.Ancient))
            .ToList();
        if (candidates.Count == 0)
            return;

        var generated = new CardModel[Amount];
        var rng = player.RunState.Rng.CombatCardGeneration;
        for (int index = 0; index < Amount; index++)
        {
            generated[index] = CardFactory.GetDistinctForCombat(player, candidates, 1, rng).First();
            generated[index].AddKeywordInternal(CardKeyword.Ethereal);
        }

        foreach (CardModel card in generated)
            await CardPileCmd.Generate(Owner.CombatState!, card, PileType.Hand, player);
    }
}
