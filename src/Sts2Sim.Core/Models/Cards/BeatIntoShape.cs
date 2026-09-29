using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BeatIntoShape : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        5m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        2m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override int CanonicalStarCost => -1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal forgePerHit = IsUpgraded ? 7m : 5m;
        var attack = await DamageCmd.Attack(forgePerHit)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
        int ownerHitsThisTurn = CombatState!.DamageHistory.Entries.Count(entry =>
            entry.HappenedThisTurn(CombatState) &&
            ReferenceEquals(entry.Receiver, cardPlay.Target) &&
            ReferenceEquals(entry.Dealer, Owner.Creature) &&
            entry.Result.Props.IsPoweredAttack());
        int currentAttackHits = attack.Results.SelectMany(hit => hit).Count();
        decimal forge = forgePerHit + forgePerHit * (ownerHitsThisTurn - currentAttackHits);
        await ForgeCmd.Forge(forge, Owner, this);
    }
}
