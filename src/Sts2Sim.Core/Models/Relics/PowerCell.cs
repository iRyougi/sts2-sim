using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PowerCell : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState!.TurnNumber > 1)
            return Task.CompletedTask;

        Flash();
        List<CardModel> source = Owner.PlayerCombatState.DrawPile.Cards
            .Where(card => !card.CostsXEnergy && card.LocalEnergyCost == 0).ToList();
        // v0.113.1：非 Innate 与 Innate 两组各自 StableShuffle（各消耗一次父流），非 Innate 在前拼接后取前 2 张。
        IEnumerable<CardModel> cards = source.Where(card => !card.HasKeyword(CardKeyword.Innate)).ToList()
            .StableShuffle(Owner.RunState.Rng.CombatCardSelection)
            .Concat(source.Where(card => card.HasKeyword(CardKeyword.Innate)).ToList()
                .StableShuffle(Owner.RunState.Rng.CombatCardSelection))
            .Take(2);
        foreach (CardModel card in cards)
            CardPileCmd.Add(card, PileType.Hand);
        return Task.CompletedTask;
    }
}
