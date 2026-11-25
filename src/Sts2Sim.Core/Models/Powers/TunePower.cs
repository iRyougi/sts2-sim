using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>每回合首次打出 Ki 牌后，随机升级手牌中层数张可升级牌（v0.114.0 原版 <c>TunePower</c>）。</summary>
public sealed class TunePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card.Owner, Owner.Player) ||
            !cardPlay.Card.Tags.Contains(CardTag.Ki) ||
            !MelodyCmd.IsFirstPlayThisTurn(cardPlay, CardTag.Ki))
        {
            return Task.CompletedTask;
        }

        Entities.Players.Player player = Owner.Player!;
        List<CardModel> toUpgrade = player.PlayerCombatState!.Hand.Cards
            .Where(card => card.IsUpgradable)
            .TakeRandom(Amount, player.RunState.Rng.CombatCardSelection)
            .ToList();
        foreach (CardModel card in toUpgrade)
        {
            CardCmd.Upgrade(card);
        }

        return Task.CompletedTask;
    }
}
