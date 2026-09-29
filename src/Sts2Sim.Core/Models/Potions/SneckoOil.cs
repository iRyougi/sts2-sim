using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class SneckoOil : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player
            ?? throw new InvalidOperationException("Snecko Oil requires a player target.");
        ICombatState combatState = target.CombatState!;
        await CardPileCmd.Draw(combatState, 7, player, fromHandDraw: false);

        // 原版只给 EnergyCost.GetWithModifiers(None) >= 0 的牌抽费用：该值是不含临时修正的基础费用，
        // 不可打出的状态/诅咒牌为 -1，被跳过且不消耗随机数。写入用 SetThisTurnOrUntilPlayed，打出即失效。
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            if (!card.CostsXEnergy && card.CanonicalEnergyCostValue >= 0)
            {
                card.SetTemporaryCostOverrideThisTurnOrUntilPlayed(
                    combatState.RunState.Rng.CombatEnergyCosts.NextInt(4));
            }
        }
    }
}
