using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Potions;

internal static class CardChoicePotionEffect
{
    public static async Task GenerateOneOfThree(
        ICombatState combatState,
        Player recipient,
        Player creator,
        CardPoolModel pool,
        Func<CardModel, bool> isEligible,
        PotionModel source)
    {
        // Native OnUse passes the recipient's ordered pool to GetDistinctForCombat:
        // filter, shuffle the complete eligible pool, then take three (N-1 RNG draws).
        IReadOnlyList<CardModel> generated = CardFactory.GetDistinctForCombat(
            recipient,
            pool.GetUnlockedCards(
                recipient.UnlockState,
                isMultiplayer: recipient.RunState.Players.Count > 1).Where(isEligible),
            3,
            recipient.RunState.Rng.CombatCardGeneration);

        // Generated candidates stay outside all piles until the chosen instance is generated.
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(combatState, recipient, generated, 0, 1, source, cancelable: true)).FirstOrDefault();
        if (selected is not null)
        {
            selected.MakeTemporaryFreeThisTurn();
            await CardPileCmd.Generate(combatState, selected, PileType.Hand, creator);
        }
    }
}
