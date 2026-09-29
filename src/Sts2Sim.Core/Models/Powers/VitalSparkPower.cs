namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

public sealed class VitalSparkPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task BeforeCombatStart()
    {
        foreach (CardModel card in PlayerSkills())
        {
            await CardCmd.Afflict<Tainted>(card, Amount);
        }
    }

    public override Task AfterCardEnteredCombat(CardModel card) =>
        card.Type == CardType.Skill && card.Affliction is null
            ? CardCmd.Afflict<Tainted>(card, Amount)
            : Task.CompletedTask;

    public override Task AfterCardPlayed(CardPlay cardPlay) =>
        cardPlay.Card.Affliction is Tainted
            ? PowerCmd.Apply<TaintedPower>(
                Owner.CombatState!,
                cardPlay.Card.Owner.Creature,
                Amount,
                applier: null,
                cardSource: null)
            : Task.CompletedTask;

    public override Task AfterRemoved(Creature oldOwner)
    {
        // Death cleanup may detach the creature before removing its powers.
        if (oldOwner.CombatState is null) return Task.CompletedTask;

        foreach (CardModel card in PlayerCards(oldOwner))
        {
            if (card.Affliction is Tainted)
            {
                CardCmd.ClearAffliction(card);
            }
        }

        return Task.CompletedTask;
    }

    public override Task AfterPowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(power, this))
        {
            foreach (CardModel card in PlayerSkills().Where(card => card.Affliction is Tainted))
            {
                card.Affliction!.SetAmount(Amount);
            }
        }

        return Task.CompletedTask;
    }

    private IEnumerable<CardModel> PlayerSkills() =>
        PlayerCards(Owner).Where(card => card.Type == CardType.Skill);

    private static IEnumerable<CardModel> PlayerCards(Creature source) => source.CombatState!
        .Allies
        .Where(creature => creature.Player?.PlayerCombatState is not null)
        .SelectMany(creature => creature.Player!.PlayerCombatState!.AllPiles)
        .SelectMany(pile => pile.Cards)
        .Distinct();
}
