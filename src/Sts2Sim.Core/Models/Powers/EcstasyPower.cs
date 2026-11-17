using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>玩家回合开始时随机将手中至多叠加层数张 Sadness 变形为 Joy（原版 <c>EcstasyPower</c>）。</summary>
public sealed class EcstasyPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player))
            return;

        List<Sadness> candidates = player.PlayerCombatState!.Hand.Cards.OfType<Sadness>().ToList();
        if (candidates.Count == 0)
            return;

        candidates.StableShuffle(player.RunState.Rng.CombatCardSelection);
        var transformations = new List<CardTransformation>();
        foreach (Sadness sadness in candidates.Take(Amount))
        {
            var joy = (CardModel)ModelDb.Card<Joy>().MutableClone();
            joy.AssignOwner(player);
            transformations.Add(new CardTransformation(sadness, joy));
        }

        await CardCmd.Transform(transformations, null);
    }
}
