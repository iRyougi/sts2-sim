using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TheBall : GeneratedCardModel
{
    private decimal _damage = 10m;

    public override bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    // Real card: MultiplayerConstraint == MultiplayerOnly.
    public override bool IsMultiplayerOnly => true;
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        10m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override int CanonicalStarCost => -1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
        _damage += IsUpgraded ? 15m : 10m;
    }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        CardLocation fallback = base.GetResultLocationForCardPlay();
        if (CombatState is null)
        {
            return fallback;
        }

        Player[] recipients = CombatState.Allies
            .Where(creature => creature.IsAlive && !ReferenceEquals(creature.Player, Owner))
            .Select(creature => creature.Player)
            .OfType<Player>()
            .ToArray();
        if (recipients.Length == 0)
        {
            return fallback;
        }

        Player recipient = Owner.RunState.Rng.CombatTargets.NextItem(recipients)
            ?? throw new InvalidOperationException("Random teammate selection returned no recipient.");
        return fallback.PileType == PileType.Discard
            ? new CardLocation(recipient, PileType.Draw, CardPilePosition.Random)
            : fallback with { Player = recipient };
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(_damage);
}
