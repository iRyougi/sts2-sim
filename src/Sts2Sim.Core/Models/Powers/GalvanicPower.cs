namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

public sealed class GalvanicPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeCombatStart()
    {
        foreach (Creature ally in Owner.CombatState!.Allies.Where(ally => ally.Player is not null))
        {
            foreach (CardModel card in CombatCards(ally.Player!))
            {
                if (card.Type == CardType.Power && card.Affliction is null)
                {
                    await CardCmd.Afflict<Galvanized>(card, Amount);
                }
            }
        }
    }

    public override Task AfterCardEnteredCombat(CardModel card) =>
        card.Type == CardType.Power && card.Affliction is null
            ? CardCmd.Afflict<Galvanized>(card, Amount)
            : Task.CompletedTask;

    public override Task AfterCardPlayed(CardPlay cardPlay) =>
        cardPlay.Card.Affliction is Galvanized
            ? CreatureCmd.Damage(Owner.CombatState!, new[] { cardPlay.Card.Owner.Creature }, Amount,
                Sts2Sim.Core.ValueProps.ValueProp.Unpowered | Sts2Sim.Core.ValueProps.ValueProp.Move,
                null, null, null)
            : Task.CompletedTask;

    private static IEnumerable<CardModel> CombatCards(Entities.Players.Player player) =>
        player.PlayerCombatState!.DrawPile.Cards
            .Concat(player.PlayerCombatState.Hand.Cards)
            .Concat(player.PlayerCombatState.DiscardPile.Cards)
            .Concat(player.PlayerCombatState.ExhaustPile.Cards)
            .Concat(player.PlayerCombatState.PlayPile.Cards);
}
