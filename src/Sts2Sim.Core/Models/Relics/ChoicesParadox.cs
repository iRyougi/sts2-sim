using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ChoicesParadox : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // 原版挂在 AfterPlayerTurnStart（抽完手牌后、AfterSideTurnStart 之前），同一时机的遗物先后与随机数消耗顺序才和原版一样
    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner || Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        IReadOnlyList<CardModel> choices = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.Players.Count > 1),
            5,
            Owner.RunState.Rng.CombatCardGeneration);
        foreach (CardModel choice in choices)
        {
            choice.AddKeywordInternal(CardKeyword.Retain);
        }

        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromSimpleGrid(
            combatState, Owner, choices, 1, 1, this);
        if (selected.FirstOrDefault() is { } card)
        {
            await CardPileCmd.Generate(combatState, card, PileType.Hand);
        }
    }
}
