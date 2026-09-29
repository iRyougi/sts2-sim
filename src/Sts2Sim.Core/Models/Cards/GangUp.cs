using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class GangUp : GeneratedCardModel
{
    // Native CalculatedDamage.Calculate(null) finds no damage entry with a null receiver.
    public override bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = 5m;
        return true;
    }

    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        5m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override int CanonicalStarCost => -1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int teammateHits = CombatState!.DamageHistory.Entries.Count(entry =>
            entry.HappenedThisTurn(CombatState) &&
            ReferenceEquals(entry.Receiver, cardPlay.Target) &&
            entry.Dealer is not null &&
            !ReferenceEquals(entry.Dealer, Owner.Creature) &&
            entry.Dealer.Side == Owner.Creature.Side &&
            entry.Result.Props.IsPoweredAttack());
        decimal extraDamage = IsUpgraded ? 7m : 5m;
        await DamageCmd.Attack(5m + extraDamage * teammateHits)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
    }
}
