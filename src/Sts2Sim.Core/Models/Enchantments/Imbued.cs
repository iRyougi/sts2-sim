using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Enchantments;

/// <summary>
/// Uses the same Skill-only identity introduced by Task 5 commit 7aabe48, extended with the
/// authoritative initial-bottom placement and first-turn autoplay lifecycle required by Electric Shrymp.
/// </summary>
public sealed class Imbued : EnchantmentModel
{
    public override bool ShouldStartAtBottomOfDrawPile => true;

    public override bool CanEnchant(CardModel card) =>
        base.CanEnchant(card) && card.Type == CardType.Skill;

    // 原版 Imbued 没有 ModifyShuffleOrder：开局放到底部由 Player.ArrangeDrawPileForFirstHand 在第 1 回合抽牌前做。

    public override Task AfterAutoPrePlayPhaseEntered(Player player)
    {
        if (player != Owner.Owner || player.PlayerCombatState?.TurnNumber > 1 || Owner.CombatState is null)
            return Task.CompletedTask;

        return AutoPlayCmd.FromCards(Owner.CombatState, player, [Owner]);
    }
}
