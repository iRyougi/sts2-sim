using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>摸牌前按原生整池洗牌生成等于层数的无色卡。</summary>
public sealed class SpectrumShiftPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        ICombatState combatState = Owner.CombatState!;
        IEnumerable<CardModel> candidates = ColorlessCardPool.Instance.GetUnlockedCards(
            player.UnlockState, player.RunState.Players.Count > 1);

        foreach (CardModel clone in CardFactory.GetDistinctForCombat(
            player, candidates, Amount, combatState.RunState.Rng.CombatCardGeneration))
        {
            await CardPileCmd.Generate(combatState, clone, PileType.Hand);
        }
    }
}
