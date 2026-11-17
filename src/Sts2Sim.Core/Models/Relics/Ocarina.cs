using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>第一回合抽牌前从已解锁角色池生成 1 张旋律牌入手牌（原版 <c>Ocarina</c>）。</summary>
public sealed class Ocarina : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player == Owner &&
            Owner.PlayerCombatState?.TurnNumber <= 1 &&
            Owner.Creature.CombatState is { } combatState)
        {
            bool isMultiplayer = Owner.RunState.Players.Count > 1;
            List<CardModel> candidates = Owner.UnlockState.CharacterCardPools
                .SelectMany(pool => pool.GetUnlockedCards(Owner.UnlockState, isMultiplayer))
                .Where(c => c.Tags.Contains(CardTag.Wah) || c.Tags.Contains(CardTag.Cho) || c.Tags.Contains(CardTag.Ki))
                .ToList();
            if (candidates.Count > 0)
            {
                List<CardModel> generated = CardFactory.GetDistinctForCombat(
                    Owner, candidates, 1, Owner.RunState.Rng.CombatCardGeneration).ToList();
                Flash();
                foreach (CardModel card in generated)
                {
                    await CardPileCmd.Generate(combatState, card, PileType.Hand, Owner);
                }
            }
        }
    }
}
