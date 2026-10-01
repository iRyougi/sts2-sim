using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class WhisperingEarring : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;

    public override async Task AfterAutoPrePlayPhaseEnteredLate(Player player)
    {
        if (player != Owner || player.PlayerCombatState is not { } playerCombatState ||
            playerCombatState.TurnNumber > 1 || Owner.Creature.CombatState is not { } combatState) return;
        int startTurn = playerCombatState.TurnNumber;
        using IDisposable selector = CardSelectCmd.PushSelector(VakuuCardSelector.Instance);
        for (int index = 0; index < 13; index++)
        {
            if (combatState.IsOverOrEnding() ||
                combatState is CombatState { Engine: { } engine } && engine.IsPlayerReadyToEndTurn(player) ||
                !player.Creature.IsAlive || playerCombatState.TurnNumber != startTurn) return;
            CardModel? card = playerCombatState.Hand.Cards.FirstOrDefault(candidate => candidate.CanPlay(out _));
            if (card is null) return;
            Creature? target = card.TargetType switch
            {
                TargetType.AnyEnemy => combatState.HittableEnemies.FirstOrDefault(),
                TargetType.AnyAlly => combatState.RunState.Rng.CombatTargets.NextItem(
                    CombatTargetCandidates.ForCard(combatState, Owner, TargetType.AnyAlly)),
                TargetType.AnyPlayer => Owner.Creature,
                _ => null,
            };
            PrepaidXCapture xCapture = await card.PrepayResourcesForAutoplayAsync();
            await AutoPlayCmd.FromPrepaidCard(combatState, Owner, card, target, xCapture);
        }
    }

    private sealed class VakuuCardSelector : ICardSelectionDecisionSource
    {
        public static VakuuCardSelector Instance { get; } = new();

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>(request.Candidates.Take(request.MaxCount).ToArray());
    }
}
