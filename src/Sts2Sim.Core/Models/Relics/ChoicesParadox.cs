using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ChoicesParadox : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState?.TurnNumber != 1)
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

        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            combatState, Owner, choices, 1, 1, this);
        if (selected.FirstOrDefault() is { } card)
        {
            await CardPileCmd.Generate(combatState, card, PileType.Hand);
        }
    }
}
