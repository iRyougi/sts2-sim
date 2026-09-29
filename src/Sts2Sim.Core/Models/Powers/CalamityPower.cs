using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Marks owned Attacks before resolution, then generates the current Amount after each resolves.</summary>
public sealed class CalamityPower : PowerModel
{
    private HashSet<CardPlay> _eligibleCardPlays = new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner.Creature == Owner && cardPlay.Card.Type == CardType.Attack)
        {
            _eligibleCardPlays.Add(cardPlay);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!_eligibleCardPlays.Remove(cardPlay))
        {
            return;
        }

        Player player = Owner.Player!;
        IEnumerable<CardModel> unlockedAttacks = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1)
            .Where(card => card.Type == CardType.Attack);
        IReadOnlyList<CardModel> generated = CardFactory.GetForCombat(
            player,
            unlockedAttacks,
            Amount,
            Owner.CombatState!.RunState.Rng.CombatCardGeneration);
        foreach (CardModel card in generated)
        {
            await CardPileCmd.Generate(Owner.CombatState, card, PileType.Hand, Owner.Player);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _eligibleCardPlays = new HashSet<CardPlay>(_eligibleCardPlays, ReferenceEqualityComparer.Instance);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(_eligibleCardPlays.Count == 0, nameof(_eligibleCardPlays));
    }
}
