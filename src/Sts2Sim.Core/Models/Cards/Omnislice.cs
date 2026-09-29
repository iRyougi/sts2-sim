using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Omnislice : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        0, 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        8m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        3m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = Spec.Damage + (IsUpgraded ? Spec.UpgradeDamage : 0m);
        await using AttackContext context = await AttackCommand.CreateContextAsync(CombatState!, cardPlay);
        IReadOnlyList<DamageResult> primaryResults = await CreatureCmd.Damage(
            CombatState!, [cardPlay.Target], damage, ValueProp.Move,
            Owner.Creature, this, cardPlay);
        context.AddHit(primaryResults);
        DamageResult? primaryResult = primaryResults.FirstOrDefault();
        if (primaryResult is null)
        {
            return;
        }

        Creature[] teammates = CombatState!
            .GetCreaturesOnSide(primaryResult.Receiver.Side)
            .Where(creature => creature != cardPlay.Target && creature.IsHittable)
            .ToArray();
        if (teammates.Length == 0)
        {
            return;
        }

        IReadOnlyList<DamageResult> splashResults = await CreatureCmd.Damage(
            CombatState,
            teammates,
            primaryResult.TotalDamage + primaryResult.OverkillDamage,
            ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature,
            this,
            cardPlay);
        context.AddHit(splashResults);
    }
}
